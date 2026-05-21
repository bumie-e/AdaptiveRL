import cv2
import numpy as np
import os
from pathlib import Path

def mask_to_yolo_polygons(mask_path, class_id=0):
    # Load mask as grayscale
    mask = cv2.imread(str(mask_path), cv2.IMREAD_GRAYSCALE)
    if mask is None:
        return ""

    # Threshold to ensure binary
    _, binary = cv2.threshold(mask, 127, 255, cv2.THRESH_BINARY)

    # Find contours
    contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    yolo_labels = []
    h, w = mask.shape

    for contour in contours:
        # Simplify contour to reduce number of points
        epsilon = 0.002 * cv2.arcLength(contour, True)
        approx = cv2.approxPolyDP(contour, epsilon, True)
        
        if len(approx) < 3:
            continue

        # Flatten and normalize
        points = approx.reshape(-1, 2)
        normalized_points = []
        for x, y in points:
            normalized_points.append(f"{x/w:.6f} {y/h:.6f}")
        
        yolo_labels.append(f"{class_id} " + " ".join(normalized_points))

    return "\n".join(yolo_labels)

def process_dataset(unity_data_path, output_path):
    unity_data_path = Path(unity_data_path)
    output_path = Path(output_path)
    
    # YOLO directory structure
    (output_path / "images").mkdir(parents=True, exist_ok=True)
    (output_path / "labels").mkdir(parents=True, exist_ok=True)

    mask_files = list((unity_data_path / "masks").glob("*.png"))
    print(f"Found {len(mask_files)} masks. Converting...")

    for mask_path in mask_files:
        # Expected image name: mask_123_456.png -> img_123_456.png
        img_name = mask_path.name.replace("mask_", "img_")
        img_src = unity_data_path / "images" / img_name
        
        if not img_src.exists():
            print(f"Warning: Image {img_name} not found for mask {mask_path.name}")
            continue

        # Get polygons
        label_content = mask_to_yolo_polygons(mask_path)
        
        if label_content:
            # Save Label
            label_name = mask_path.stem.replace("mask_", "img_") + ".txt"
            label_path = output_path / "labels" / label_name
            with open(label_path, "w") as f:
                f.write(label_content)
            
            # Copy Image (linked by name) if source and dest are different
            img_dest = output_path / "images" / img_name
            if img_src.absolute() != img_dest.absolute():
                import shutil
                shutil.copy(img_src, img_dest)

    print(f"Done! YOLO dataset ready at: {output_path}")

if __name__ == "__main__":
    # Pointing to the dataset already in the DetectionServer folder
    UNITY_DATA = "./yolo_dataset"
    OUTPUT_YOLO_DATA = "./yolo_dataset"
    process_dataset(UNITY_DATA, OUTPUT_YOLO_DATA)
