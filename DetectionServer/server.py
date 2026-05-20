from fastapi import FastAPI, File, UploadFile
from pydantic import BaseModel
import cv2
import numpy as np
from ultralytics import YOLO
import io
from PIL import Image

app = FastAPI()

# Load YOLOv8-seg or YOLOv11-seg (Nano)
# For the first run, this will download the pre-trained COCO model
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
            # We assume classes are mapped (e.g., 0 might be waste if fine-tuned)
            # For now, we look for any detected object
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

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
