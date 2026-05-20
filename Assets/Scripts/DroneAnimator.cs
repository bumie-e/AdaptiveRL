using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DroneAnimator : MonoBehaviour {
	List<Transform> Props = new List<Transform>();
	public float propSpeed = 1000f;
	Transform trans;
	public float bobSpeed = 2f;
	public float bobHeight = 0.2f;
	public float wobble = 1.5f;
	public float wobbleSpeed = 1.5f;

	void Start () {
		trans = transform;
		foreach (Transform child in transform) {
			if (child.name.ToLower().Contains ("motor")) {
				if (child.childCount > 0) {
					Props.Add(child.GetChild(0));
				} else {
					Props.Add(child);
				}
			}
		}
	}
	
	void Update () {
		foreach (Transform prop in Props) {
			if (prop != null) {
				prop.Rotate (0, 0, propSpeed * Time.deltaTime);
			}
		}

		Vector3 pos = trans.localPosition;
		pos.y += Mathf.Sin (Time.time * bobSpeed) * bobHeight * Time.deltaTime;
		trans.localPosition = pos;

		Vector3 rot = trans.localEulerAngles;
		rot.x = 0 + Mathf.Sin (Time.time * wobbleSpeed) * wobble;
		rot.z = 0 + Mathf.Cos (Time.time * wobbleSpeed) * wobble;
		trans.localEulerAngles = rot;
	}
}
