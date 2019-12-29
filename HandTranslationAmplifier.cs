using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;

// Statuses for the amplification calibration process.
// -  Inactive: Amplification is not running.
// -  calibrateComfort: Calibration is at "set comfort" step
// -  calibrateMax: Calibration is at "set max" step
// -   active: Amplification is calibrated and running.

public enum AmpStatus {
    inactive, calibrateComfort, calibrateMax, active
}

// Which of the hands are visible?
public enum HandVisibility {
    amplified, real, both, none
}

public class HandTranslationAmplifier : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject leftHand, rightHand, leftShoulder, rightShoulder, amplifiedLeft, amplifiedRight, invertedSpherePrefab;

    // In the scene, these status canvases show instructions for the calibration process
    public TextMesh leftStatusText, rightStatusText;

    // The actual curve to be used for amplification. Can be set in the Inspector or just made programmatically.
    public AnimationCurve ampCurve;

    // This is kind of hacky. These gameobjects are spheres with inverted normals, with the radius set to as long as the
    // user's arm. This allows us to raycast from the comfort point, through the hand, to the edge of the sphere and 
    // infer a max reach point from the user's arm length. 
    GameObject leftReachSphere, rightReachSphere;

    // The comfort point P_n. 
    [HideInInspector]
    public GameObject leftComfortPoint, rightComfortPoint;

    // Amplification status - left hand
    [HideInInspector]
    public AmpStatus leftStatus = AmpStatus.inactive;

    // Amplification status - right hand
    [HideInInspector]
    public AmpStatus rightStatus = AmpStatus.inactive;

    // Visibility settings for the controllers
    public HandVisibility displayControllers = HandVisibility.amplified;
    HandVisibility originalControllerSetting;

    // initial max reach
    [HideInInspector]
    public float target_maxDistance = 0.75f;

    // prints stuff to the console if true
    public bool verboseInput = false;

    // Name of the VR headset
    private string headsetName = OVRPlugin.productName ?? "None";

    // used for the experiment - an array of amplification curves that we cycled through
    public AnimationCurve[] curves;
    int curveIndex = -1;

    float baseKeyTime = 0.7f;

    // Input handling 
    bool rightTriggerDown {
        get {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)){
                if (verboseInput) print("Right Trigger");
                return true;
            }
            return false;
        }
    }

    bool leftTriggerDown {
        get {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)){
                if (verboseInput) print("Left Trigger");
                return true;
            }
            return false;
        }
    }

    bool aDown {
        get {
            if ((headsetName.StartsWith("Oculus Rift") && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            || (!headsetName.StartsWith("Oculus Rift") && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))){
                if (verboseInput) print("Right One");
                return true;
            }
            return false;
        }
    }

    bool xDown {
        get {
            if ((headsetName.StartsWith("Oculus Rift") && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
            || (!headsetName.StartsWith("Oculus Rift") && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))){
                if (verboseInput) print("Left One");
                return true;
            }
            return false;
        }
    }

    bool bDown {
        get {
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)){
                if (verboseInput) print("Right Two");
                return true;
            }
            return false;
        }
    }

    bool yDown {
        get {
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)){
                if (verboseInput) print("Left Two");
                return true;
            }
            return false;
        }
    }


    void Start()
    {

        amplifiedLeft.tag = "AmplifiedLeft";
        amplifiedRight.tag = "AmplifiedRight";

        originalControllerSetting = displayControllers;
        setControllerVisibility(displayControllers);

        leftStatusText.gameObject.transform.parent = leftHand.transform;
        leftStatusText.gameObject.transform.localPosition = new Vector3(-0.07f,0.01f,0.115f);
        leftStatusText.gameObject.transform.localRotation = Quaternion.Euler(77f,19f,17f);
        leftStatusText.text = "Press X to calibrate";

        rightStatusText.transform.parent = rightHand.transform;
        rightStatusText.transform.localPosition = new Vector3(-0.07f,0.01f,0.115f);
        rightStatusText.gameObject.transform.localRotation = Quaternion.Euler(77f,19f,17f);
        rightStatusText.text = "Press A to calibrate";

        // THIS WAS USED IN OUR EXPERIMENT. We generated a bunch of curves to run through.
        // AnimationCurve[] newCurves = CurveTester.shared.generateCurves();
        // for (int i = 1; i < newCurves.Length; i++){
        //     if (i < curves.Length){
        //         curves[i] = newCurves[i];
        //     }
        // }

    }

    // Update is called once per frame
    void Update()
    {
        if (originalControllerSetting != displayControllers){
            setControllerVisibility(displayControllers);
        }

        if (xDown){
            leftStatus = AmpStatus.calibrateComfort;
            leftStatusText.text = "Press Trigger at\ncomfortable position";
        }
        if (aDown){
            rightStatus = AmpStatus.calibrateComfort;
            rightStatusText.text = "Press Trigger at\ncomfortable position";
        }

        checkCalibration();
        amplify(HandSide.left);
        amplify(HandSide.right);
        
    }

    void checkCalibration(){
        if (leftTriggerDown && (leftStatus == AmpStatus.calibrateComfort || leftStatus == AmpStatus.calibrateMax)){
            if (leftStatus == AmpStatus.calibrateComfort){
                Destroy(leftComfortPoint);
                leftComfortPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leftComfortPoint.GetComponent<SphereCollider>().enabled = false;
                leftComfortPoint.name = "Left Comfort Point";
                leftComfortPoint.transform.localScale = new Vector3(0.01f,0.01f,0.01f);
                leftComfortPoint.transform.position = leftHand.transform.position;
                leftComfortPoint.transform.parent = leftShoulder.transform;
                leftStatus = AmpStatus.calibrateMax;
                leftStatusText.text = "Press Trigger at\nmax reach";
            }
            else {
                if (leftReachSphere != null) {
                    Destroy(leftReachSphere);
                }

                leftReachSphere = Instantiate(invertedSpherePrefab);
                float maxReachDistance = Vector3.Distance(leftShoulder.transform.position,leftHand.transform.position);
                leftReachSphere.transform.parent = leftShoulder.transform;
                leftReachSphere.transform.localPosition = Vector3.zero;
                leftReachSphere.transform.localScale = new Vector3(200 * maxReachDistance, 200 * maxReachDistance, 200 * maxReachDistance);
                leftReachSphere.layer = 22;
                leftReachSphere.GetComponent<Renderer>().enabled = false;
                leftStatus = AmpStatus.active;
                leftStatusText.text = "";
            }
        }

        if (rightTriggerDown && (rightStatus == AmpStatus.calibrateComfort || rightStatus == AmpStatus.calibrateMax)){
            if (rightStatus == AmpStatus.calibrateComfort){
                Destroy(rightComfortPoint);
                rightComfortPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rightComfortPoint.GetComponent<SphereCollider>().enabled = false;
                rightComfortPoint.name = "Right Comfort Point";
                rightComfortPoint.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                rightComfortPoint.transform.position = rightHand.transform.position;
                rightComfortPoint.transform.parent = rightShoulder.transform;
                rightStatus = AmpStatus.calibrateMax;
                rightStatusText.text = "Press Trigger at\nmax reach";
            }
            else {
                if (rightReachSphere != null) {
                    Destroy(rightReachSphere);
                }

                rightReachSphere = Instantiate(invertedSpherePrefab);
                float maxReachDistance = Vector3.Distance(rightShoulder.transform.position,rightHand.transform.position);
                target_maxDistance = maxReachDistance;
                rightReachSphere.transform.parent = rightShoulder.transform;
                rightReachSphere.transform.localPosition = Vector3.zero;
                rightReachSphere.transform.localScale = new Vector3(200 * maxReachDistance, 200 * maxReachDistance, 200 * maxReachDistance);
                rightReachSphere.layer = 23;
                rightReachSphere.GetComponent<Renderer>().enabled = false;
                rightStatus = AmpStatus.active;
                rightStatusText.text = "";
            }
        }

    }

    void amplify(HandSide side){
        if ((side == HandSide.left ? leftStatus : rightStatus) == AmpStatus.active){

            // Raycast out to the arm sphere, amplify with that hit point as the maximum.
            RaycastHit hit;
            
            int layerMask = 1 << (side == HandSide.left ? 22 : 23);
            Vector3 comfortPosition = (side == HandSide.left ? leftComfortPoint.transform.position : rightComfortPoint.transform.position);
            GameObject hand = (side == HandSide.left ? leftHand : rightHand);
            GameObject amplifiedHand = (side == HandSide.left ? amplifiedLeft : amplifiedRight);
            GameObject reachSphere = (side == HandSide.left ? leftReachSphere : rightReachSphere);
            Vector3 newPosition = hand.transform.position;

            if (Physics.Raycast(comfortPosition, (hand.transform.position - comfortPosition).normalized, out hit, Mathf.Infinity, layerMask)){
                newPosition = interpolatePosition(side, reachSphere.transform.position,comfortPosition, hand.transform.position, reachSphere.transform.localScale.x, hit.point);
            }
            
            amplifiedHand.transform.position = newPosition;

        }
    }

    // Sets controller models to be visible or not
    public void setControllerVisibility(HandVisibility visibility){
        foreach(Renderer r in leftHand.GetComponentsInChildren<Renderer>()){
            r.enabled = (visibility == HandVisibility.real || visibility == HandVisibility.both);
        }
        foreach(Renderer r in rightHand.GetComponentsInChildren<Renderer>()){
            r.enabled = (visibility == HandVisibility.real || visibility == HandVisibility.both);
        }

        foreach(Renderer r in amplifiedLeft.GetComponentsInChildren<Renderer>()){
            r.enabled = (visibility == HandVisibility.amplified || visibility == HandVisibility.both);
        }
        foreach(Renderer r in amplifiedRight.GetComponentsInChildren<Renderer>()){
            r.enabled = (visibility == HandVisibility.amplified || visibility == HandVisibility.both);
        }

        originalControllerSetting = visibility;
        if (displayControllers != visibility){
            displayControllers = visibility;
        }
    }

    // Interpolates the position of the hand between the comfort and max points to amplify input.
    public Vector3 interpolatePosition(HandSide side, Vector3 shoulderPos, Vector3 comfortCenter, Vector3 controllerPosition, float comfortRadius, Vector3 maxHitPoint)
    {
        var currentReach = Vector3.Distance(comfortCenter, controllerPosition);
        var maxReach = Vector3.Distance(comfortCenter, maxHitPoint);
        var percentReach = currentReach / maxReach;
        float amplifiedOffset = ampCurve.Evaluate(percentReach);
        float magnitude = amplifiedOffset * maxReach;
        float shoulderDistance = Vector3.Distance(shoulderPos, controllerPosition);
    
        return comfortCenter + (maxHitPoint - comfortCenter).normalized * magnitude;
    }

    // shift control point to the left by increaseAmount
    public void increaseCurveIntensity(float increaseAmount){
        var keys = ampCurve.keys;
        var controlPoint = keys[1];
        ampCurve.RemoveKey(1);
        ampCurve.AddKey(controlPoint.time - increaseAmount,controlPoint.value);
    }

    // This is for moving the control points of the RNL curve programmatically.
    public void setCurveIntensity(float amount){
        
        var keys = ampCurve.keys;
        var controlPoint = keys[1];
        
        ampCurve.MoveKey(1,new Keyframe(baseKeyTime-amount,controlPoint.value));
        ampCurve.MoveKey(2,new Keyframe(1, 1 + (0.1f*amount/.3f)));

        AnimationUtility.SetKeyBroken(ampCurve,2,true);
        AnimationUtility.SetKeyLeftTangentMode(ampCurve,2,AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyRightTangentMode(ampCurve,2,AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyLeftTangentMode(ampCurve,1,AnimationUtility.TangentMode.Auto);
        AnimationUtility.SetKeyRightTangentMode(ampCurve,1,AnimationUtility.TangentMode.Auto);
        AnimationUtility.SetKeyRightTangentMode(ampCurve,0,AnimationUtility.TangentMode.Linear);
        AnimationUtility.SetKeyBroken(ampCurve,1,false);

        print($"IN: {ampCurve.keys[1].inTangent}");
        print($"OUT: {ampCurve.keys[1].outTangent}");
    }

    // Set curve from the array of curves
    public void setCurve(int index){
        ampCurve = curves[index];
        curveIndex = index;
    }
}
