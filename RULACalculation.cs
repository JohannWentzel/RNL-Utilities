using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// How are RULA points being tracked: IK, Kinect, not tracked
public enum RULAJointSource {
	IKModel, Kinect, none
}
public class RULAManager : MonoBehaviour {

    // The body points needed.
	public GameObject head, leftController, rightController, leftHand, rightHand,leftShoulder,rightShoulder,leftElbow,rightElbow, leftWrist, rightWrist, waist;
	
	private GameObject bodyFlushLeft, bodyFlushRight;

	int leftShoulderVal, rightShoulderVal, leftLower, rightLower, leftWristVal, rightWristVal, leftWristTwist, rightWristTwist, neck,trunk;
	
    // This table contains the results from the Posture Score A table. Supply indices to the array like this:
	// upperArm-1, wrist-1, wristTwist-1, lower-1
	private int[,,,] upperBodyMapping = new int[6,4,2,3] {
		{
			{
				{1,2,2},
				{2,2,3},
			},
			{
				{2,2,3},
				{2,2,3}
			},
			{
				{2,3,3},
				{3,3,3}
			},
			{

				{3,3,4},
				{3,3,4}
			}
		},
		{
			{    
				{2,3,3},
				{3,3,4}
			},
			{
				{3,3,4},
				{3,3,4}
			},
			{
				{3,3,4},
				{4,4,4}
			},
			{
				{4,4,5},
				{4,4,5}
			}
		},
		{
			{
				{3,3,4},
				{3,4,4}
			},
			{
				{4,4,4},
				{4,4,4}
			},
			{
				{4,4,4},
				{4,4,5}
			},
			{
				{5,5,5},
				{5,5,5}
			}
		},
		{
			{
				{4,4,4},
				{4,4,4}
			},
			{
				{4,4,4},
				{4,4,5}
			},
			{
				{4,4,5},
				{5,5,5}
			},
			{
				{5,5,6},
				{5,5,6}
			}
		},
		{
			{
				{5,5,6},
				{5,6,6}
			},
			{
				{5,6,6},
				{5,6,7}
			},
			{
				{5,6,7},
				{6,7,7}
			},
			{
				{6,7,7},
				{7,7,8}
			}
		},
		{
			{
				{7,8,9},
				{7,8,9}
			},
			{
				{7,8,9},
				{7,8,9}
			},
			{
				{7,8,9},
				{8,9,9}
			},
			{
				{8,9,9},
				{9,9,9}
			}
		}
	}; 

	// trunk-1, neck-1
	private int[,] lowerBodyMapping = new int[6,6]{
		{1,2,3,5,7,8},
		{2,2,3,5,7,8},
		{3,4,4,6,7,8},
		{5,5,5,7,8,8},
		{6,6,6,7,8,9},
		{7,7,7,8,8,9}
	};

    [HideInInspector]
    public float leftElbowAngle;
	
    [HideInInspector]
    public float rightElbowAngle;
	
    [HideInInspector]
    public float leftShoulderAngle;
	
    [HideInInspector]
    public float rightShoulderAngle;
	
    [HideInInspector]
    public float leftWristAngle, rightWristAngle;

    // The angle offset based on the neutral holding position of each controller (e.g. what angle is most comfortable to hold a Vive controller)
    // Oculus offset was set to 0.
	public float controllerAngleOffset = 30.0f;
	public RULAJointSource source = RULAJointSource.none;

    // Want more print statements? Have more print statements.
	public bool verbose = false;

    // fallback values in case bdoy tracking is lost (we had that happen now and then with the Kinect)
	GameObject fallback_leftHand, fallback_rightHand,fallback_leftShoulder,fallback_rightShoulder,fallback_leftElbow,fallback_rightElbow, fallback_leftWrist, fallback_rightWrist, fallback_waist;

	public int leftRULA {
		get {
			return getUpperRULA(HandSide.left);
		}
	}
	public int rightRULA {
		get {
			return getUpperRULA(HandSide.right);
		}
	}

	bool allAssigned {
        get {
            return (
                head != null
                && leftShoulder != null
                && rightShoulder != null
                && leftElbow != null
                && rightElbow != null
                && waist != null
                && leftHand != null
                && rightHand != null
            );
        }
    }

	void Start(){
		bodyFlushLeft = new GameObject("Body Flush Left");
		bodyFlushRight = new GameObject("Body Flush Right");
		if (allAssigned) populateFallbacks();
	}

	

	// Use this for initialization
	void Update () {
		
		if (!allAssigned){
			Debug.LogError("Lost body tracking, falling back to IK");
			fallBack();
			return;
		}

		bodyFlushLeft.transform.position = new Vector3(leftShoulder.transform.position.x, leftShoulder.transform.position.y -1, leftShoulder.transform.position.z);
		bodyFlushRight.transform.position = new Vector3(rightShoulder.transform.position.x, rightShoulder.transform.position.y -1, rightShoulder.transform.position.z);

		leftElbowAngle = getAngle(leftWrist,leftElbow,leftShoulder);
		rightElbowAngle = getAngle(rightWrist,rightElbow,rightShoulder);
		leftShoulderAngle = 180 - getAngle(leftElbow, leftShoulder, bodyFlushLeft);
		rightShoulderAngle = 180 - getAngle(rightElbow,rightShoulder,bodyFlushRight);


		leftWristAngle = Mathf.Abs(Vector3.Angle(leftHand.transform.forward, leftWrist.transform.position - leftElbow.transform.position) - controllerAngleOffset);
		rightWristAngle = Mathf.Abs(Vector3.Angle(rightHand.transform.forward, rightWrist.transform.position - rightElbow.transform.position) - controllerAngleOffset);
		

		Debug.DrawLine(leftHand.transform.position, leftHand.transform.position + leftHand.transform.forward * 0.5f, Color.red);
		Debug.DrawLine(rightHand.transform.position, rightHand.transform.position + rightHand.transform.forward * 0.5f, Color.red);
		// print($"left shoulder: {leftShoulderAngle} | right shoulder: {rightShoulderAngle} | left elbow: {leftElbowAngle} | right elbow: {rightElbowAngle} | left wrist: {leftWristAngle} | right wrist: {rightWristAngle}");
		
		updateRotations();
		updateRULA();

		if (verbose) print($"RULA - left: {leftRULA} | right: {rightRULA}");

		
	}

	public void updateRotations(){
		// sets joint rotations for RULA calculation since Kinect points always report as vector3.zero
		if (source != RULAJointSource.Kinect) return;

		Vector3 waistForward = Vector3.Cross(leftShoulder.transform.position - rightShoulder.transform.position,waist.transform.position - leftShoulder.transform.position).normalized;
		Vector3 waistUp = head.transform.position - waist.transform.position;
		waist.transform.rotation = Quaternion.LookRotation(waistForward,waistUp);
		leftHand.transform.rotation = leftController.transform.rotation;
		rightHand.transform.rotation = rightController.transform.rotation;
	}

	void populateFallbacks(){
		fallback_leftHand = leftHand;
		fallback_rightHand = rightHand;
		fallback_leftShoulder = leftShoulder;
		fallback_rightShoulder = rightShoulder;
		fallback_leftElbow = leftElbow;
		fallback_rightElbow = rightElbow;
		fallback_leftWrist = leftWrist;
		fallback_rightWrist = rightWrist;
		fallback_waist = waist;
	}

	void fallBack(){
		leftHand = fallback_leftHand;
		rightHand = fallback_rightHand;
		leftShoulder = fallback_leftShoulder;
		rightShoulder = fallback_rightShoulder;
		leftElbow = fallback_leftElbow;
		rightElbow = fallback_rightElbow;
		leftWrist = fallback_leftWrist;
		rightWrist = fallback_rightWrist;
		waist = fallback_waist;
	}

	public int getUpperRULA(HandSide side){
		int rula = -1;
		if (!allAssigned) return rula;

		try {
			if (side == HandSide.left) {
				rula = upperBodyMapping[leftShoulderVal-1,leftWristVal-1,leftWristTwist-1,leftLower-1];
			} 
			else {
				rula = upperBodyMapping[rightShoulderVal-1,rightWristVal-1,rightWristTwist-1,rightLower-1];
			}
		} catch {
			Debug.LogWarning($"INVALID UPPER RULA");
		}
		return rula;
	}
	public int getLowerRULA(){
		if (!allAssigned) return -1;
		try {
			int rula = lowerBodyMapping[trunk-1,neck-1];
			return rula;
		} catch {
			Debug.LogWarning("INVALID LOWER RULA");
			return -1;
		}
	}

	float getAngleBetweenForwardVectors(Vector3 a, Vector3 b){

		var angleA = Mathf.Atan2(a.y, a.z);
        var angleB = Mathf.Atan2(b.y, b.z);
		var angleDiff = Mathf.DeltaAngle( angleA, angleB );        
        angleDiff *= Mathf.Rad2Deg;

        return angleDiff;
	}
 
	float getAngle(GameObject obj1, GameObject obj2, GameObject obj3){
		Vector3 vec1 = obj1.transform.position - obj2.transform.position;
		Vector3 vec2 = obj2.transform.position - obj3.transform.position;
		Debug.DrawLine(obj1.transform.position,obj2.transform.position,Color.gray);
		Debug.DrawLine(obj2.transform.position,obj3.transform.position,Color.gray);
		float diff = Vector3.Angle(vec1, vec2); 
		return diff;
	}

    // Checks angle values and updates RULA accordingly.
	void updateRULA(){

		// Debug.DrawRay(waist.transform.position,Vector3.Cross(leftShoulder.transform.position - rightShoulder.transform.position,waist.transform.position - leftShoulder.transform.position).normalized,Color.red);

		// left shoulder
		if (leftShoulderAngle < 20){
			leftShoulderVal = 1;
		} 
		else if (leftShoulderAngle < 45){
			leftShoulderVal = 2;
		}
		else if (leftShoulderAngle < 90){
			leftShoulderVal = 3;
		}
		else {
			leftShoulderVal = 4;
		}

		// right shoulder
		if (rightShoulderAngle < 20){
			rightShoulderVal = 1;
		} 
		else if (rightShoulderAngle < 45){
			rightShoulderVal = 2;
		}
		else if (rightShoulderAngle < 90){
			rightShoulderVal = 3;
		}
		else {
			rightShoulderVal = 4;
		}

		// left elbow
		if (leftElbowAngle >= 60 && leftElbowAngle <= 100){
			leftLower  = 1;
		}
		else {
			leftLower  = 2;
		}

		// right elbow
		if (rightElbowAngle >= 60 && rightElbowAngle <= 100){
			rightLower  = 1;
		}
		else {
			rightLower  = 2;
		}

		// left wrist
		if (Mathf.Abs(leftWristAngle) < 5){
			leftWristVal  = 1;
		}
		else if (Mathf.Abs(leftWristAngle) < 15){
			leftWristVal  = 2;
		}
		else {
			leftWristVal  = 3;
		}

		// right wrist
		if (Mathf.Abs(rightWristAngle) < 10){
			rightWristVal  = 1;
		}
		else if (Mathf.Abs(rightWristAngle) < 15){
			rightWristVal  = 2;
		}
		else {
			rightWristVal  = 3;
		}

		leftWristTwist = 1;
		rightWristTwist = 1;

		Vector3 waistRightVector = waist.transform.right;
		if (Vector3.Dot(rightShoulder.transform.position - waist.transform.position, waistRightVector) < 0){
			waistRightVector *= -1;
		}

		Vector3 waistToRightHand = rightHand.transform.position - waist.transform.position;
		Vector3 rightShoulderToRightHand = rightHand.transform.position - rightShoulder.transform.position;

		if (Vector3.Dot(waistRightVector,waistToRightHand) < 0) {
			if (verbose) print("RIGHT CROSSED BODY MIDLINE");
			rightLower += 1;
		}
		else if (Vector3.Dot(rightShoulderToRightHand, waistRightVector) > 0){
			if (verbose) print("RIGHT OUT TO SIDE");
			rightLower += 1;
		}

		Vector3 waistToLeftHand = leftHand.transform.position - waist.transform.position;
		Vector3 leftShoulderToLeftHand = leftHand.transform.position - leftShoulder.transform.position;

		if (Vector3.Dot(waistRightVector,waistToLeftHand) > 0) {
			if (verbose) print("LEFT CROSSED BODY MIDLINE");
			leftLower += 1;
		}
		else if (Vector3.Dot(leftShoulderToLeftHand, waistRightVector) < 0){
			if (verbose) print("LEFT OUT TO SIDE");
			leftLower += 1;
		}

		Vector3 waistToHead = head.transform.position - waist.transform.position;

		float trunkAngle = Vector3.SignedAngle(waistToHead,Vector3.up,waist.transform.right);
		if (verbose) print($"trunkAngle: {trunkAngle}");
		if (trunkAngle <= 0){
			trunk  = 1;
		}
		else if (trunkAngle <= 20){
			trunk  =2 ;
		}
		else if (trunkAngle <= 60){
			trunk  = 3;
		}
		else {
			trunk  = 4;
		}

		Vector3 leftShouldertoRightShoulder = rightShoulder.transform.position - leftShoulder.transform.position;
		float twistAngle = Mathf.Abs(Vector3.SignedAngle(leftShouldertoRightShoulder,waist.transform.right,Vector3.up));
		if (twistAngle > 10){
			if (verbose) print($"trunk twist - angle = {twistAngle}");
			trunk += 1;
		}
		float trunkTiltAngle = Mathf.Abs(Vector3.SignedAngle(leftShouldertoRightShoulder,waist.transform.right,waist.transform.right));
		if (trunkTiltAngle > 10){
			trunk += 1;
			if (verbose) print($"trunk side bend - angle = {trunkTiltAngle}");
		}
		
		float neckTiltAngle = Vector3.SignedAngle(waist.transform.up,head.transform.up, waist.transform.right);
		if (neckTiltAngle < 0){
			neck  = 4;
		}
		else if (neckTiltAngle < 10) {
			neck  = 1;
		}
		else if (neckTiltAngle < 20) {
			neck  = 2;
		}
		else {
			neck  = 3;
		}

		float neckTwistAngle = Mathf.Abs(Vector3.SignedAngle(waist.transform.forward,head.transform.forward,waist.transform.up));
		if (verbose) print($"neckTwistAngle = {neckTwistAngle}");
		if (neckTwistAngle > 45){
			neck += 1;
		}

		float neckRollAngle = Mathf.Abs(Vector3.SignedAngle(waist.transform.up,head.transform.up,waist.transform.up));
		if (verbose) print($"neckRollAngle = {neckRollAngle}");
		if (neckRollAngle > 45){
			neck += 1;
		}

	if (verbose) print($"LEFT UPPER: {leftShoulderVal-1}, {leftWristVal-1}, {leftWristTwist-1}, {leftLower-1}");
	if (verbose) print($"RIGHT UPPER: {rightShoulderVal-1}, {rightWristVal-1}, {rightWristTwist-1}, {rightLower-1}");

	if (verbose) print($"LeftUpperRULA: {getUpperRULA(Valve.VR.SteamVR_Input_Sources.LeftHand)}");
	if (verbose) print($"RightUpperRULA: {getUpperRULA(Valve.VR.SteamVR_Input_Sources.RightHand)}");
	}

}
