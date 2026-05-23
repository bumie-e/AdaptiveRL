
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
    public int maxSteps = 5000;
    public float forceMultiplier = 10f;
    public float torqueMultiplier = 2f;
    public Camera droneCamera;
    public bool enableDataCollection = true;
    private int stepCounter = 0;
    public string detectionServerUrl = "https://promenade-relight-refund.ngrok-free.dev/predict";
    private Vector3 lastLinearVelocity;
    private Vector3 localAcceleration;
    public RayPerceptionSensorComponent3D raySensor;
    public float ProximityPenalty = -0.01f;
    public float PlasticDetectionReward = 1f;
    public float RewardNormalisationFactor = 0.3f; // You can tune this value for stability
    public float MaxTiltAngle = 20f; // Maximum allowed tilt angle (Pitch/Roll)
    public float TiltPenalty = -0.01f; // Penalty for exceeding max tilt angle
    public float MinAltitude = 5f; // Minimum operational altitude
    public float MaxAltitude = 30f; // Maximum operational altitude
    public float AltitudePenalty = -0.02f; // Penalty for being outside altitude band
    public float ActionDeltaPenalty = -0.01f; // Penalty for large changes in actions

    private float[] previousActions = new float[4]; // Throttle, Pitch, Roll, Yaw
    Rigidbody rBody;
    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset the agent's position and velocity
        transform.localPosition = new Vector3(0, 8, 0);
        rBody.angularVelocity = Vector3.zero;
        rBody.linearVelocity = Vector3.zero;
        lastLinearVelocity = Vector3.zero;

        // Reset action history
        for (int i = 0; i < previousActions.Length; i++) previousActions[i] = 0f;

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
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(rBody.linearVelocity);
        sensor.AddObservation(this.transform.localRotation); 
        
        Vector3 localAngularVel = transform.InverseTransformDirection(rBody.angularVelocity);
        sensor.AddObservation(localAngularVel);

        Vector3 currentLinearVelocity = rBody.linearVelocity;
        localAcceleration = (currentLinearVelocity - lastLinearVelocity) / Time.fixedDeltaTime;
        lastLinearVelocity = currentLinearVelocity;
        sensor.AddObservation(transform.InverseTransformDirection(localAcceleration));

        // Action History (t-1)
        foreach (float action in previousActions)
        {
            sensor.AddObservation(action);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(ObstaclePenalty);
            Debug.Log("Hit an obstacle! Penalty applied.");
        }
        if (collision.gameObject.CompareTag("Boundary"))
        {
            Debug.Log("Touched Boundary!");
            AddReward(ObstaclePenalty);
            EndEpisode();
        }
        if (collision.gameObject.CompareTag("RiverEnd"))
        {
            Debug.Log("End of River!");
            AddReward(PlasticDetectionReward);
            EndEpisode();
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Actions, size = 4
        // [0] Throttle, [1] Pitch, [2] Roll, [3] Yaw
        float throttle = actionBuffers.ContinuousActions[0];
        float pitch = actionBuffers.ContinuousActions[1];
        float roll = actionBuffers.ContinuousActions[2];
        float yaw = actionBuffers.ContinuousActions[3];

        // Calculate Action Delta Penalty (Smoothness)
        float actionDelta = 0f;
        for (int i = 0; i < 4; i++)
        {
            actionDelta += Mathf.Pow(actionBuffers.ContinuousActions[i] - previousActions[i], 2);
        }
        AddReward(ActionDeltaPenalty * actionDelta);

        // Update action history
        for (int i = 0; i < 4; i++)
        {
            previousActions[i] = actionBuffers.ContinuousActions[i];
        }

        // Apply Flight Controls
        rBody.AddForce(transform.up * throttle * forceMultiplier);
        rBody.AddForce(transform.forward * pitch * forceMultiplier);
        rBody.AddForce(transform.right * roll * forceMultiplier);
        rBody.AddTorque(transform.up * yaw * torqueMultiplier);

        AddReward(-0.0001f);

        if (StepCount >= maxSteps)
        {
            EndEpisode();
        }

        TrackRaySensors();
        EnforceFlightStability();
        
        stepCounter++;
        if (enableDataCollection && stepCounter % 5 == 0)
        {
            StartCoroutine(CaptureAndSendImage());
        }
    }

    private void TrackRaySensors()
    {
        if (raySensor == null) return;
        var rayOutput = raySensor.GetRayPerceptionInput();
        var rayResults = RayPerceptionSensor.Perceive(rayOutput, false);
        foreach (var ray in rayResults.RayOutputs){
            if (ray.HasHit && ray.HitGameObject != null){
                if (ray.HitGameObject.CompareTag("Obstacle") || ray.HitGameObject.CompareTag("Boundary"))
                {
                    float proximityEffect = 1.0f - ray.HitFraction;
                    AddReward(ProximityPenalty * proximityEffect);
                    float hitDistance = ray.HitFraction * rayOutput.RayLength;
                    Debug.Log("Obstacle detected by ray sensor at distance: " + hitDistance);
                }
            }
        }
    }

    private void EnforceFlightStability()
    {
        Vector3 localEuler = transform.localRotation.eulerAngles;
        float pitchAngle = Mathf.Abs(NormalizeAngle(localEuler.x));
        float rollAngle = Mathf.Abs(NormalizeAngle(localEuler.z));

        if (pitchAngle > MaxTiltAngle)
        {
            AddReward(TiltPenalty * (pitchAngle - MaxTiltAngle) / MaxTiltAngle);
            Debug.Log($"Excessive Pitch: {pitchAngle} degrees. Penalty applied.");
        }
        if (rollAngle > MaxTiltAngle)
        {
            AddReward(TiltPenalty * (rollAngle - MaxTiltAngle) / MaxTiltAngle);
            Debug.Log($"Excessive Roll: {rollAngle} degrees. Penalty applied.");
        }

        float altitude = transform.localPosition.y;
        if (altitude < MinAltitude)
        {
            AddReward(AltitudePenalty * (MinAltitude - altitude) / MinAltitude);
            Debug.Log($"Too Low Altitude: {altitude} meters. Penalty applied.");
        }
        else if (altitude > MaxAltitude)
        {
            AddReward(AltitudePenalty * (altitude - MaxAltitude) / MaxAltitude);
            Debug.Log($"Too High Altitude: {altitude} meters. Penalty applied.");
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        else if (angle < -180f) angle += 360f;
        return angle;
    }

    private IEnumerator CaptureAndSendImage()
    {
        RenderTexture renderTexture = new RenderTexture(224, 224, 24);
        droneCamera.targetTexture = renderTexture;
        Texture2D screenshot = new Texture2D(224, 224, TextureFormat.RGB24, false);
        droneCamera.Render();
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, 224, 224), 0, 0);
        screenshot.Apply();
        droneCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        byte[] imageBytes = screenshot.EncodeToPNG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "screenshot.png", "image/png");

        UnityWebRequest request = UnityWebRequest.Post(detectionServerUrl, form);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            var response = JsonUtility.FromJson<PredictionResponse>(json);
            float normalizedScore = response.scores * RewardNormalisationFactor;
            AddReward(normalizedScore);
            Debug.Log($"Vision Based Detection: {response.classes}, Score: {response.scores}, Normalized: {normalizedScore}");
        }
        else
        {
            Debug.LogWarning($"Detection server error: {request.responseCode} {request.error}");
        }

        Destroy(screenshot);
    }

    public class CertificateHand : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    [System.Serializable]
    public class PredictionResponse
    {
        public string predictions;
        public string classes;
        public float scores;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[1] = Input.GetAxis("Vertical");   // Pitch
        continuousActionsOut[2] = Input.GetAxis("Horizontal"); // Roll

        float throttle = 0f;
        if (Input.GetKey(KeyCode.Space)) throttle = 1f;
        if (Input.GetKey(KeyCode.LeftShift)) throttle = -1f;
        continuousActionsOut[0] = throttle;

        float yaw = 0f;
        if (Input.GetKey(KeyCode.Q)) yaw = -1f;
        if (Input.GetKey(KeyCode.E)) yaw = 1f;
        continuousActionsOut[3] = yaw;
    }
}
