using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class CaptureCameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public GameObject drone;
    public Vector3 offset = new Vector3(0, 5, -10); 
    public bool lookAtDrone = true;

    [Header("Data Collection")]
    public bool enableDataCollection = true;
    public int captureIntervalFrames = 5;
    public Shader segmentationShader;
    
    private int frameCounter = 0;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) Debug.LogError("CaptureCameraController: No Camera component found!");
    }

    void LateUpdate()
    {
        if (drone == null) return;

        transform.position = drone.transform.position + drone.transform.TransformDirection(offset);
        if (lookAtDrone) transform.LookAt(drone.transform);

        frameCounter++;
        if (enableDataCollection && frameCounter % captureIntervalFrames == 0)
        {
            CaptureData();
        }
    }

    private void CaptureData()
    {
        string timestamp = $"{Time.frameCount}_{System.DateTime.Now:HHmmss}";
        string dataPath = Path.Combine(Application.dataPath, "Data");
        string imagesPath = Path.Combine(dataPath, "images");
        string masksPath = Path.Combine(dataPath, "masks");

        if (!Directory.Exists(imagesPath)) Directory.CreateDirectory(imagesPath);
        if (!Directory.Exists(masksPath)) Directory.CreateDirectory(masksPath);

        CaptureView(Path.Combine(imagesPath, $"img_{timestamp}.png"), null);

        if (segmentationShader != null)
        {
            CaptureView(Path.Combine(masksPath, $"mask_{timestamp}.png"), segmentationShader);
        }
    }

    private void CaptureView(string fullPath, Shader replacementShader)
    {
        RenderTexture rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;

        if (replacementShader != null)
            cam.RenderWithShader(replacementShader, "RenderType");
        else
            cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        screenShot.Apply();

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);

        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(screenShot);
    }
}
