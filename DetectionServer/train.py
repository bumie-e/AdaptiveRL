from ultralytics import YOLO
import os

def train_model():
    # Load the Nano segmentation model (YOLOv11 or YOLOv8)
    # YOLOv11-seg is the latest, but v8 is very stable
    model = YOLO('yolo11n-seg.pt') 

    # Training configuration
    model.train(
        data='data.yaml',
        epochs=50,
        imgsz=640,
        batch=16,
        device=0,  # Use 0 for GPU, 'cpu' for CPU
        project='WasteDetection',
        name='Nigeria_Project_v1'
    )

    print("Training complete. Your best model is in: WasteDetection/Nigeria_Project_v1/weights/best.pt")

if __name__ == "__main__":
    # Ensure the dataset exists before training
    if not os.path.exists('yolo_dataset'):
        print("Error: yolo_dataset not found. Please run convert_masks.py first.")
    else:
        train_model()
