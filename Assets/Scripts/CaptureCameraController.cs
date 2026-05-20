using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO; // For file saving

public class CaptureCameraController : MonoBehaviour
{
    // Reference to the drone GameObject
    public GameObject drone;

    // Offset position of the camera relative to the drone
    public Vector3 offset = new Vector3(0, -1, 0);

    // Flag to enable/disable picture-taking functionality
    public bool enableDataCollection = true;

    // Counter to track frames for picture-taking intervals
    private int frameCounter = 0;

    // Update is called once per frame
    void LateUpdate()
    {
        if (drone == null)
        {
            Debug.LogWarning("Drone reference is not assigned!");
            return;
        }

        // Update the camera's position to follow the drone with the specified offset
        transform.position = drone.transform.position + offset;

        // Optional: Make the camera look at the drone
        // transform.LookAt(drone.transform);
        // Increment the frame counter
        frameCounter++;

        // Take a picture every 5 frames if data collection is enabled
        if (enableDataCollection && frameCounter % 5 == 0)
        {
            TakePicture();
        }
    }

    // Method to take a picture of the environment
    private void TakePicture()
    {
        Camera camera = GetComponent<Camera>();
        if (camera == null)
        {
            Debug.LogWarning("No Camera component found on this GameObject!");
            return;
        }

        // Create a RenderTexture and set it as the camera's target
        RenderTexture renderTexture = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = renderTexture;

        // Render the camera's view
        Texture2D screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        camera.Render();

        // Read the pixels from the RenderTexture
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenshot.Apply();

        // Reset the camera's target texture and RenderTexture
        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        // Save the image to a file
        byte[] bytes = screenshot.EncodeToPNG();
        string filePath = Path.Combine(Application.dataPath, "Screenshots");
        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }
        string fileName = $"screenshot_{Time.frameCount}.png";
        File.WriteAllBytes(Path.Combine(filePath, fileName), bytes);

        Debug.Log($"Screenshot saved to {Path.Combine(filePath, fileName)}");
    }
}