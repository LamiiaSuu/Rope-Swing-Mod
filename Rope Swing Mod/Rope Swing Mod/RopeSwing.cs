using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

[BepInPlugin("com.lamia.ropeswing", "RopeSwing", "0.6.3")]
public class RopeSwing : BaseUnityPlugin
{
    internal static ConfigEntry<float> BaseSwingForce;
    internal static ConfigEntry<float> JointAngularZLimit;
    internal static ConfigEntry<float> RopeMass;
    internal static ConfigEntry<float> SegmentYouAreOnRopeMass;
    internal static ConfigEntry<float> RopeLengthSpeedMultiplierPerSegment;
    internal static ConfigEntry<KeyCode> SwingForward;
    internal static ConfigEntry<KeyCode> SwingBackwards;
    internal static ConfigEntry<KeyCode> SwingLeft;
    internal static ConfigEntry<KeyCode> SwingRight;
    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo("RopeSwing loaded.");
        Harmony harmony = new Harmony("com.lamia.ropeswing");
        harmony.PatchAll();
        SwingForward = Config.Bind("RopeSwing", "FlyKey", KeyCode.LeftShift, "Key to activate swinging forward.");
        SwingBackwards = Config.Bind("RopeSwing", "SwingBackwards", KeyCode.LeftControl, "Key to activate swinging backwards.");
        SwingLeft = Config.Bind("RopeSwing", "SwingLeft", KeyCode.A, "Key to activate swinging left.");
        SwingRight = Config.Bind("RopeSwing", "SwingRight", KeyCode.D, "Key to activate swinging right.");
        BaseSwingForce = Config.Bind("RopeSwing", "BaseForce", 7.5f, "Base force applied when swinging forward/backward/sideways.");
        JointAngularZLimit = Config.Bind("RopeSwing", "JointAngularZLimit", 25f, "The Joint restrictions on ropes.");
        RopeMass = Config.Bind("RopeSwing", "RopeMass", 0.15f, "The mass of every rope segment.");
        SegmentYouAreOnRopeMass = Config.Bind("RopeSwing", "SegmentYouAreOnRopeMass", 0.35f, "The mass of the rope segment you are currently touching. This should be higher than the normal ropes mass.");
        RopeLengthSpeedMultiplierPerSegment = Config.Bind("RopeSwing", "RopeLengthSpeedMultiplierPerSegment", 0.35f, "How much will the speed multiplier increase with each segment of the rope.");
    }
}

[HarmonyPatch(typeof(Character), "Awake")]
public static class RopeSwingMod
{
    [HarmonyPostfix]
    public static void AwakePatch(Character __instance)
    {
        if (!__instance.IsLocal)
            return;
        if ((Object)(object)((Component)__instance).GetComponent<RopeSwingPatch>() == (Object)null)
        {

            ((Component)__instance).gameObject.AddComponent<RopeSwingPatch>();
            RopeSwing.Log.LogInfo((object)("RopeSwingPatch added to: " + ((Object)__instance).name));
        }
    }
}


[HarmonyPatch(typeof(CharacterRopeHandling), "Update")]
class Patch_CharacterRopeHandling_Update_SetMaxAngle
{
    static void Prefix(CharacterRopeHandling __instance)
    {
        __instance.maxRopeAngle = 150f;  
    }
}


public class RopeSwingPatch : MonoBehaviourPun
{
    private Character character;
    private CharacterMovement charMovement;
    private bool jumpedOff = false;

    private void Start()
    {
        character = ((Component)this).GetComponent<Character>();
        charMovement = this.GetComponent<CharacterMovement>();
    }

    private void Update()
    {
        if (!character.IsLocal)

            return;


        if (character.data.isRopeClimbing)
        {
            jumpedOff = true;

            if (Input.GetKey(RopeSwing.SwingForward.Value) || Input.GetKey(RopeSwing.SwingBackwards.Value) || Input.GetKey(RopeSwing.SwingLeft.Value) || Input.GetKey(RopeSwing.SwingRight.Value))
            {



                Vector3 swingForce = character.data.lookDirection_Flat;

                

                if (Input.GetKey(RopeSwing.SwingForward.Value))
                {
                    swingForce *= RopeSwing.BaseSwingForce.Value;
                }

                else if (Input.GetKey(RopeSwing.SwingBackwards.Value))
                {
                    swingForce *= -RopeSwing.BaseSwingForce.Value;
                }



                Vector3 right = Vector3.Cross(Vector3.up, character.data.lookDirection_Flat).normalized * RopeSwing.BaseSwingForce.Value;

                if (Input.GetKey(RopeSwing.SwingRight.Value))
                {

                    swingForce += right;

                }


                if (Input.GetKey(RopeSwing.SwingLeft.Value))
                {

                    swingForce -= right;

                }


                List<Transform> segments = character.data.heldRope.GetRopeSegments();
                /*
                Rigidbody rb = segments[segments.Count-2].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.mass = 2f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.AddForceAtPosition(swingForce, character.data.climbPos, ForceMode.Force);
                    RopeSwing.Log.LogInfo("[RopeMod] Segment " + rb);
                }
                */



                Rigidbody rbs = character.data.heldRope.gameObject.GetComponent<RopeClimbingAPI>().GetSegmentFromPercent(character.data.ropePercent).GetComponent<Rigidbody>();
                bool isBelowPos = false;
                foreach (var segment in segments)
                {
                    float lengthMultiplier = 1f;
                    Rigidbody rb = segment.GetComponent<Rigidbody>();
                    
                    if (rb != null)
                    {
                        
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.maxAngularVelocity = 0.1f;
                        rb.maxLinearVelocity = 55f;
                        rb.mass = RopeSwing.RopeMass.Value;
                        ConfigurableJoint joint = segment.GetComponent<ConfigurableJoint>();
                        joint.angularZMotion = ConfigurableJointMotion.Limited;
                        joint.angularZLimit = new SoftJointLimit { limit = RopeSwing.JointAngularZLimit.Value };

                        if (!isBelowPos)
                            rb.AddForce((swingForce / 4) * lengthMultiplier + (Vector3.down * (RopeSwing.BaseSwingForce.Value * lengthMultiplier)), ForceMode.Force);
                        else
                        {

                        }
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                        rb.solverIterations = 20;
                        rb.solverVelocityIterations = 20;
                        
                        if (rb.Equals(rbs))
                            isBelowPos = true;
                        //rb.angularDamping = 0.9f;
                        //rb.AddForce(Vector3.down * 25f, ForceMode.Acceleration);
                        
                        //rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                        //rb.AddForceAtPosition(swingForce*lengthMultiplier, character.data.climbPos, ForceMode.Acceleration);

                        lengthMultiplier += RopeSwing.RopeLengthSpeedMultiplierPerSegment.Value;



                    }
                    else
                    {
                        RopeSwing.Log.LogInfo("[RopeMod] Segment " + rb + " has no Rigidbody. Cannot apply physics effects.");
                    }
                }
                
                if (rbs != null)
                {
                    
                    foreach (var part in character.refs.ragdoll.partList)
                    {


                        //if (part.partType.ToString().Contains("Hand"))
                        //{
                        part.AddForce(swingForce *0.5f, ForceMode.Force);
                        //part.transform.position = rbs.transform.position;
                        //}
                    }
                    
                    rbs.AddForce(swingForce, ForceMode.Force);
                    rbs.mass = RopeSwing.SegmentYouAreOnRopeMass.Value;
                    RopeSwing.Log.LogInfo("[RopeMod] Segment " + rbs);
                }





            }

            else
            {
                
            }


        }
        /*
        else if (jumpedOff)
        {
            
            
                jumpedOff = false;
                foreach (var part in character.refs.ragdoll.partList)
                {


                //if (!part.partType.ToString().Contains("Head") && !part.partType.ToString().Contains("Arm") && !part.partType.ToString().Contains("Elbow") && !part.partType.ToString().Contains("Torso") && !part.partType.ToString().Contains("Finger") && !part.partType.ToString().Contains("Shoulder"))
                //{
                    part.AddForce((RopeSwing.BaseSwingForce.Value*20)*character.data.lookDirection, ForceMode.Acceleration);
                //}



            }
            RopeSwing.Log.LogMessage("Character pushed " + (RopeSwing.BaseSwingForce.Value * 2000) * character.data.lookDirection);

        }

        */
    }
}

