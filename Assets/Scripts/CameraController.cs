
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO; // For file saving
public class CameraController : MonoBehaviour
{
    // Reference to the player GameObject.
    public GameObject player;

    // The distance between the camera and the player.
    private Vector3 offset;
    public bool enableDataCollection = false;

    // Counter to track frames for picture-taking intervals
    private int frameCounter = 0;

    // Start is called before the first frame update.
    void Start()
    {
        // Calculate the initial offset between the camera's position and the player's position.
        offset = transform.position - player.transform.position;
    }

    // LateUpdate is called once per frame after all Update functions have been completed.
    void LateUpdate()
    {
        // Maintain the same offset between the camera and player throughout the game.
        transform.position = player.transform.position + offset;
        // Optional: Make the camera look at the drone
        // transform.LookAt(drone.transform);
        // Increment the frame counter
        frameCounter++;

        // // Take a picture every 5 frames if data collection is enabled
        // if (enableDataCollection && frameCounter % 8 == 0)
        // {
        //     TakePicture();
        // }
        
    }

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