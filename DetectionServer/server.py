import os
from fastapi import FastAPI, File, UploadFile
from pydantic import BaseModel
import cv2
import numpy as np
from ultralytics import YOLO
import io
from PIL import Image
from pyngrok import ngrok
import uvicorn

app = FastAPI()

# Load YOLOv8-seg or YOLOv11-seg (Nano)
model = YOLO('yolov8n-seg.pt') 

class DetectionResponse(BaseModel):
    predictions: str
    classes: str
    scores: float
    area_pixels: int

@app.post("/predict")
async def predict(file: UploadFile = File(...)):
    # Read image
    contents = await file.read()
    image = Image.open(io.BytesIO(contents)).convert("RGB")
    img_array = np.array(image)

    # Inference
    results = model(img_array, conf=0.25)
    
    # Process results
    best_score = 0.0
    best_class = "none"
    total_area = 0
    
    for r in results:
        if r.masks is not None:
            for i, box in enumerate(r.boxes):
                score = float(box.conf[0])
                if score > best_score:
                    best_score = score
                    best_class = r.names[int(box.cls[0])]
                
                # Sum up pixels in masks for area estimation
                mask = r.masks.data[i].cpu().numpy()
                total_area += np.sum(mask > 0.5)

    return {
        "predictions": "detected",
        "classes": best_class,
        "scores": best_score,
        "area_pixels": int(total_area)
    }

def start_tunnel(port):
    # Check for ngrok authtoken in environment
    auth_token = os.environ.get("NGROK_TOKEN")
    if auth_token:
        ngrok.set_auth_token(auth_token)

    # Check for static domain
    domain = os.environ.get("NGROK_DOMAIN")

    try:
        if domain:
            public_url = ngrok.connect(port, domain=domain).public_url
        else:
            public_url = ngrok.connect(port).public_url

        print(f"\n[NGROK] Tunnel started! Public URL: {public_url}")
        print(f"[NGROK] Forwarding: http://localhost:{port} -> {public_url}")
        return public_url
    except Exception as e:
        print(f"\n[NGROK] Failed to start tunnel: {e}")
        print("[NGROK] Tip: Ensure you have an account at ngrok.com and set your NGROK_TOKEN environment variable.")
        if domain:
            print(f"[NGROK] Tip: Also verify that the domain '{domain}' is reserved in your ngrok dashboard.")
        return None

if __name__ == "__main__":
    PORT = 8000
    
    # Optional: Set this to True to start the tunnel automatically
    # Or run with: USE_NGROK=true python server.py
    USE_NGROK = os.environ.get("USE_NGROK", "false").lower() == "true"
    
    if USE_NGROK:
        start_tunnel(PORT)
        
    uvicorn.run(app, host="0.0.0.0", port=PORT)
