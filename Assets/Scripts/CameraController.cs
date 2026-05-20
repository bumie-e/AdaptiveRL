
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public GameObject drone;
    public Vector3 offset = new Vector3(0, 0.5f, 1f); 
    public bool followRotation = true;

    [Header("Data Collection")]
    public bool enableDataCollection = true;
    public int captureIntervalFrames = 5;
    public Shader segmentationShader;
    public LayerMask plasticLayer; 
    
    private int frameCounter = 0;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) Debug.LogError("CameraController: No Camera component found!");
    }

    void LateUpdate()
    {
        if (drone == null) return;

        if (followRotation)
        {
            transform.position = drone.transform.position + drone.transform.TransformDirection(offset);
            transform.rotation = drone.transform.rotation;
        }
        else
        {
            transform.position = drone.transform.position + offset;
        }

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

        int originalMask = cam.cullingMask;
        Color originalBG = cam.backgroundColor;
        CameraClearFlags originalFlags = cam.clearFlags;

        if (replacementShader != null)
        {
            cam.cullingMask = plasticLayer; 
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.RenderWithShader(replacementShader, ""); 
        }
        else
        {
            cam.Render(); 
        }

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        screenShot.Apply();

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);

        cam.targetTexture = null;
        cam.cullingMask = originalMask;
        cam.backgroundColor = originalBG;
        cam.clearFlags = originalFlags;
        RenderTexture.active = null;
        
        Destroy(rt);
        Destroy(screenShot);
    }
}
