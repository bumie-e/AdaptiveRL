
using UnityEngine;
using UnityEngine.Networking; // Add this at the top
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections; // Needed for IEnumerator
using System.IO; // Needed for Directory and File

public class DroneAgent : Agent
{
    // Assign targets and their corresponding rewards
    public Transform Target; // List of targets in the scene
    public float ObstaclePenalty = -0.5f; // Penalty for hitting an obstacle
    public float rewardThreshold = 2f; // Threshold for the reward
    // LayerMask to filter raycast hits (optional, if you want to limit detection to specific layers).
    public LayerMask plasticLayer;
    public int maxSteps = 5000;
    public float forceMultiplier = 3f;
    public Camera droneCamera;
    public bool enableDataCollection = true;
    private int stepCounter = 0;
    public string detectionServerUrl = "http://127.0.0.1:8000/predict";


    Rigidbody rBody;
    void Start () {
        rBody = GetComponent<Rigidbody>();
    }

    // public override void Initialize()
    // {
    //     elapsedTime = 0f;
    // }
    
    public override void OnEpisodeBegin()
    {
        // Reset the agent's position and velocity

        transform.localPosition = new Vector3(0, 17, -40);
        rBody.angularVelocity = Vector3.zero;

        // Reposition all pickup objects randomly within the defined spawn area
        GameObject[] wastes = GameObject.FindGameObjectsWithTag("Plastic");
        foreach (GameObject waste in wastes)
        {
            // Randomly reposition each pickup object
            // waste.transform.localPosition = new Vector3(Random.value * 8 - 4,
            //                                              0.5f,
            //                                              Random.value * 8 - 4);
            // Reactivate the pickup if it was deactivated
            if (!waste.activeSelf)
            {
                waste.SetActive(true);
            }
            // stepCounter = 0; // Reset step counter at the start of each episode
        }

    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Target and Agent positions
        // sensor.AddObservation(Target.localPosition);
        sensor.AddObservation(this.transform.localPosition);

        // Agent velocity
        sensor.AddObservation(rBody.linearVelocity.x);
        sensor.AddObservation(rBody.linearVelocity.z);

        // Collect observations for the positions of all pickups
        // GameObject[] pickups = GameObject.FindGameObjectsWithTag("Plastic");
        // foreach (GameObject pickup in pickups)
        // {
        //     // Add the position of each pickup to the observations
        //     sensor.AddObservation(pickup.transform.localPosition);
        // }
    }
    private void OnCollisionEnter(Collision collision)
    {

        // Check if the agent collided with an obstacle
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(ObstaclePenalty); // Apply the penalty
            Debug.Log("Hit an obstacle! Penalty applied.");
        }
        // Check if the agent collided with an obstacle
        if (collision.gameObject.CompareTag("Boundary"))
        {
            Debug.Log("Touched Boundary!");
            AddReward(ObstaclePenalty); // Apply the penalty

            EndEpisode();

        }
        // Check if the agent reached the river end
        if (collision.gameObject.CompareTag("RiverEnd"))
        {
            Debug.Log("End of River!");
            AddReward(+1); // Apply the reward

            EndEpisode();
            
        }
    }

    
    // private void FixedUpdate()
    // {
        
    // }

    private void DetectPlastics()
{
    int numberOfRays = 16; // Number of rays to cast in a circular pattern
    // float radius = 5f; // Radius of the circular pattern
    float rayLength = 10f; // Length of each ray
    int count = 0;

    // Loop through each angle to cast rays in a circular pattern
    for (int i = 0; i < numberOfRays; i++)
    {
        // Calculate the angle for the current ray
        float angle = i * (360f / numberOfRays);
        Vector3 direction = Quaternion.Euler(15, angle, -15) * Vector3.down;

        // Cast the ray from the drone's position outward
        Ray ray = new Ray(transform.position, direction);
        RaycastHit hit;

        // Optional: Visualize the ray in the Scene view
        Debug.DrawRay(transform.position, direction * rayLength, Color.green);

        // Check if the ray hits an object
        if (Physics.Raycast(ray, out hit, rayLength))
        {
            

            // Check if the hit object has the "Plastic" tag
            if (hit.collider.CompareTag("Plastic"))
            {
                // Optional: Visualize the hit point in the Scene view
                // Debug.DrawRay(hit.point, hit.normal * rayLength, Color.red);

                Debug.Log("Plastic detected at: " + hit.point);

                AddReward(+1f); // Reward the agent for detecting plastic
                count ++;
                // Deactivate the plastic object
                hit.collider.gameObject.SetActive(false);
                return;
            }
        }

        if (count >= 14)
        {
            Debug.Log("All plastics have been captured!");

            AddReward(+2f); // Reward the agent for detecting plastic
            
            EndEpisode();
            return;
        }
    }
}



    // public float rotationSpeed = 20f; // Speed of rotation
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Actions, size = 2
        Vector3 controlSignal = Vector3.zero;

        controlSignal.x = actionBuffers.ContinuousActions[0];
        controlSignal.z = actionBuffers.ContinuousActions[1];

        rBody.AddForce(controlSignal * forceMultiplier);

        // Small negative reward to encourage faster task completion
        AddReward(-0.0001f);

        // Check if the agent has exceeded the maximum number of steps
        if (StepCount >= maxSteps)
        {
            // End the episode with a small penalty for timeout
            // AddReward(-0.5f);
            EndEpisode();
        }

        // Perform raycasting to detect plastics
        // DetectPlastics();

        // Calculate horizontal distance (X-Z only)
        // float horizontalDistance = Vector2.Distance(
        //     new Vector2(this.transform.position.x, this.transform.position.z),
        //     new Vector2(Target.position.x, Target.position.z)
        // );

        // // Reward based on horizontal distance
        // if (horizontalDistance <= 1f)
        // {
        //     Debug.Log("Closer Plastic Area: " + horizontalDistance);
        //     AddReward(+1);
        // }
        // else if (horizontalDistance <= rewardThreshold)
        // {
        //     Debug.Log("Within Plastic Area: " + horizontalDistance);

        //     AddReward(+0.5f);
        // }

        // ...existing code...
        stepCounter++;
        if (enableDataCollection && stepCounter % 5 == 0)
        {
            StartCoroutine(CaptureAndSendImage());
        }
        // ...existing code...

        // // Increment step counter and take a picture every 5 steps
        // stepCounter++;
        // if (enableDataCollection && stepCounter % 5 == 0)
        // {
        //     TakePicture();
        // }


    }

    // IEnumerator Upload()
    // {
    //     WWWForm form = new WWWForm();
    //     form.AddField("myField", "myData");

    //     using UnityWebRequest www = UnityWebRequest.Post("http://localhost:8000/pred", form);
    //     yield return www.SendWebRequest();

    //     if (www.result != UnityWebRequest.Result.Success)
    //     {
    //         Debug.LogError(www.error);
    //     }
    //     else
    //     {
    //         Debug.Log("Form upload complete!");
    //     }
    // }

    private IEnumerator CaptureAndSendImage()
    {
        // Capture image

        // Create a RenderTexture and set it as the camera's target
        RenderTexture renderTexture = new RenderTexture(224, 224, 24);
        droneCamera.targetTexture = renderTexture;

        // Render the camera's view
        Texture2D screenshot = new Texture2D(224, 224, TextureFormat.RGB24, false);
        droneCamera.Render();

        // Read the pixels from the RenderTexture
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, 224, 224), 0, 0);
        screenshot.Apply();

        // Reset the camera's target texture and RenderTexture
        droneCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        // Encode to PNG
        byte[] imageBytes = screenshot.EncodeToPNG();

        // Create multipart form data - IMPORTANT: field name must be "file" to match your server
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "screenshot.png", "image/png");

        // Send request
        UnityWebRequest request = UnityWebRequest.Post("http://localhost:8000/predict", form);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            Debug.Log($"Server response: {json}");

            // Parse the response
            var response = JsonUtility.FromJson<PredictionResponse>(json);
            Debug.Log($"Predictions received: {response.classes}");

            //Normalise the scores using an alpha between 0 and 1 to balance the reward score in a decreasing manner
            float alpha = 0.3f; // You can tune this value for stability
            float normalizedScore = response.scores * alpha;
            AddReward(normalizedScore);
            Debug.Log($"Vision Based Detection: {response.classes}, Score: {response.scores}, Normalized: {normalizedScore}");


            // // Use predictions for rewards or other logic
            // AddReward(response.scores); // Or use your own logic
            // Debug.Log($"Vision Based Detection: {response.classes}, Score: {response.scores}");
        }
        else
        {
            Debug.LogWarning($"Detection server error: {request.responseCode} {request.error}");
            if (request.downloadHandler != null)
            {
                Debug.LogWarning($"Response body: {request.downloadHandler.text}");
            }
        }

        // Clean up
        Destroy(screenshot);
    }

    public class CertificateHand : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    // [System.Serializable]
    // public class DetectionResult
    // {
    //     public string predictions;
    //     public List<double> classes;
    //     public List<double> scores;
    // }

    // ADD THIS NEW CLASS HERE:
    [System.Serializable]
    public class PredictionResponse
    {
        public string predictions; // If your server returns a string
        public string classes;
        public float scores;
    }



    // private IEnumerator CaptureAndSendImage()
    // {
    //     // Take screenshot from drone camera
    //     RenderTexture rt = new RenderTexture(224, 224, 24); // Use YOLO input size if possible
    //     droneCamera.targetTexture = rt;
    //     Texture2D screenshot = new Texture2D(224, 224, TextureFormat.RGB24, false);
    //     droneCamera.Render();
    //     RenderTexture.active = rt;
    //     screenshot.ReadPixels(new Rect(0, 0, 224, 224), 0, 0);
    //     screenshot.Apply();
    //     droneCamera.targetTexture = null;
    //     RenderTexture.active = null;
    //     Destroy(rt);

    //     // Encode to PNG
    //     byte[] imageBytes = screenshot.EncodeToPNG();

    //     // Send to Python server
    //     UnityWebRequest www = UnityWebRequest.Put(detectionServerUrl, imageBytes);
    //     www.method = UnityWebRequest.kHttpVerbPOST;
    //     www.SetRequestHeader("Content-Type", "application/octet-stream");

    //     yield return www.SendWebRequest();

    //     Debug.Log($"Gotten results! {www.result}");

    //     if (www.result == UnityWebRequest.Result.Success)
    //     {
    //         // Parse response (assume JSON: { "score": 0.87, "label": "plastic" })
    //         string json = www.downloadHandler.text;
    //         DetectionResult result = JsonUtility.FromJson<DetectionResult>(json);

    //         // Use the probability score to update reward
    //         AddReward((float)result.score); // Or use your own logic
    //         Debug.Log($"Vision Based Detection: {result.label}, Score: {result.score}");
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"Detection server error: {www.responseCode} {www.error}");
    //     }
    // }

    // [System.Serializable]
    // public class DetectionResult
    // {
    //     public string message;
    //     public double score;
    // }
    private void TakePicture()
    {
        if (droneCamera == null)
        {
            Debug.LogWarning("Drone camera is not assigned!");
            return;
        }

        // Create a RenderTexture and set it as the camera's target
        RenderTexture renderTexture = new RenderTexture(1920, 1080, 24);
        droneCamera.targetTexture = renderTexture;

        // Render the camera's view
        Texture2D screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        droneCamera.Render();

        // Read the pixels from the RenderTexture
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenshot.Apply();

        // Reset the camera's target texture and RenderTexture
        droneCamera.targetTexture = null;
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
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
        // continuousActionsOut[2] = Input.GetAxis("Rotation"); // For rotation control


    }
    


    


}

