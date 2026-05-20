
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
    private Vector3 lastLinearVelocity;
    private Vector3 localAcceleration;
    public RayPerceptionSensorComponent3D raySensor;
    public float ProximityPenalty = -0.01f;
    public float PlasticDetectionReward = 1f;
    public float RewardNormalisationFactor = 0.3f; // You can tune this value for stability


    Rigidbody rBody;
    void Start () {
        rBody = GetComponent<Rigidbody>();
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset the agent's position and velocity

        transform.localPosition = new Vector3(0, 17, -40);
        rBody.angularVelocity = Vector3.zero;
        rBody.linearVelocity = Vector3.zero;
        lastLinearVelocity = Vector3.zero;

        // Reposition all pickup objects randomly within the defined spawn area
        GameObject[] wastes = GameObject.FindGameObjectsWithTag("Plastic");
        foreach (GameObject waste in wastes)
        {
            if (!waste.activeSelf)
            {
                waste.SetActive(true);
            }
        }

    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Target and Agent positions
        // sensor.AddObservation(Target.localPosition);
        sensor.AddObservation(this.transform.localPosition);

        // Full 3D Agent velocity
        sensor.AddObservation(rBody.linearVelocity);
        // Local Rotation (Orientation). Helps the drone understand its tilt (Pitch/Roll)
        sensor.AddObservation(this.transform.localRotation); 
        // 2. Local Angular Velocity (Gyroscope). Helps the drone understand how fast it is rotating
        Vector3 localAngularVel = transform.InverseTransformDirection(rBody.angularVelocity);
        sensor.AddObservation(localAngularVel);
        // 3. Local Acceleration (Accelerometer)
        // Crucial for sensing external forces and impacts
        Vector3 currentLinearVelocity = rBody.linearVelocity;
        localAcceleration = (currentLinearVelocity - lastLinearVelocity) / Time.fixedDeltaTime;
        lastLinearVelocity = currentLinearVelocity;
        sensor.AddObservation(transform.InverseTransformDirection(localAcceleration));

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
            AddReward(PlasticDetectionReward); // Apply the reward
            EndEpisode();
            
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
        TrackRaySensors();
        
        // Increment step counter and take a picture every 5 steps
        stepCounter++;
        if (enableDataCollection && stepCounter % 5 == 0)
        {
            TakePicture();
        }


    }
    private void TrackRaySensors()
    {
        if (raySensor == null) return;
        var rayOutput = raySensor.GetRayPerceptionInput();
        var rayResults = RayPerceptionSensor.Perceive(rayOutput);
        foreach (var ray in rayResults.Outputs){
            if (ray.HasHit && ray.HitGameObject != null){
                if (ray.HitGameObject.CompareTag("Obstacle") || ray.HitGameObject.CompareTag("Boundary"))
                {
                    // Penalize based on how close the obstacle is (closer = higher penalty)
                    // ray.HitFraction is 0 when touching, 1 at max
                    float proximityEffect = 1.0f - ray.HitFraction;
                    AddReward(ProximityPenalty * proximityEffect);
                    Debug.Log("Obstacle detected by ray sensor at distance: " + ray.Distance);
                }
            }
        }
    }




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
        string filePath = Path.Combine(Application.dataPath, Data);
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

