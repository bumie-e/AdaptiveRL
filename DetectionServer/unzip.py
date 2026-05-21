import zipfile

# Path to your zip file
zip_path = "/home/bunmi/AdaptiveRL/Assets/Data.zip"
# Directory where you want to extract the files
extract_path = "/home/bunmi/AdaptiveRL/Assets/Data"

with zipfile.ZipFile(zip_path, 'r') as zip_ref:
    zip_ref.extractall(extract_path)
