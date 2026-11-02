using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

[BepInPlugin("com.lamia.ropeswing", "RopeSwing", "0.7.1")]
public class RopeSwing : BaseUnityPlugin
{
    internal static ConfigEntry<float> Acceleration, MaxSpeed, PumpBoost, CoastDamping, SteeringDamping, ClimberLoad;
    internal static ConfigEntry<KeyCode> Forward, Back, Left, Right;
    private Harmony harmony;

    private void Awake()
    {
        Acceleration = Config.Bind("Swing Physics", "Acceleration", 24f,
            new ConfigDescription("Swing acceleration in metres per second squared. Higher feels stronger.", new AcceptableValueRange<float>(1f, 40f)));
        MaxSpeed = Config.Bind("Swing Physics", "DriveSpeed", 10f,
            new ConfigDescription("Short-rope speed where input stops adding energy. Longer reaches scale this up with leverage. Gravity and existing momentum remain free.", new AcceptableValueRange<float>(3f, 20f)));
        PumpBoost = Config.Bind("Swing Physics", "PumpBoost", 0.2f,
            new ConfigDescription("Extra drive when pressing along the direction of travel. Reverse input brakes immediately.", new AcceptableValueRange<float>(0f, 1f)));
        CoastDamping = Config.Bind("Swing Physics", "CoastDamping", 0.12f,
            new ConfigDescription("Gentle tangential drag while occupied, in inverse seconds. Zero preserves all momentum.", new AcceptableValueRange<float>(0f, 0.5f)));
        SteeringDamping = Config.Bind("Swing Physics", "SteeringDamping", 0.65f,
            new ConfigDescription("Extra drag perpendicular to the combined input. Reduces sideways wandering without resisting the requested direction.", new AcceptableValueRange<float>(0f, 2f)));
        ClimberLoad = Config.Bind("Swing Physics", "ClimberLoad", 0.75f,
            new ConfigDescription("Extra downward acceleration from a climber, spread across nearby segments. Does not change segment mass.", new AcceptableValueRange<float>(0f, 2f)));
        Forward = Config.Bind("RopeSwing", "FlyKey", KeyCode.LeftShift, "Swing forward.");
        Back = Config.Bind("RopeSwing", "SwingBackwards", KeyCode.LeftControl, "Swing backward.");
        Left = Config.Bind("RopeSwing", "SwingLeft", KeyCode.A, "Swing left.");
        Right = Config.Bind("RopeSwing", "SwingRight", KeyCode.D, "Swing right.");
        harmony = new Harmony("com.lamia.ropeswing");
        harmony.PatchAll(typeof(RopeSwing).Assembly);
        foreach (Character character in FindObjectsByType<Character>(FindObjectsSortMode.None))
            Attach(character);
        Logger.LogInfo("RopeSwing 0.7.1 loaded. All clients need 0.7.1. Force-based climbing and cooperative swinging enabled.");
    }

    internal static void Attach(Character character)
    {
        if (!character.GetComponent<RopeSwingInput>())
            character.gameObject.AddComponent<RopeSwingInput>();
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        foreach (RopeSwingInput controls in FindObjectsByType<RopeSwingInput>(FindObjectsSortMode.None))
            controls.ResetTracking();
    }
}

[HarmonyPatch(typeof(Character), "Awake")]
internal static class AttachSwingInput
{
    private static void Postfix(Character __instance) { RopeSwing.Attach(__instance); }
}

[HarmonyPatch(typeof(CharacterRopeHandling), "Update")]
internal static class SwingClimbAngle
{
    private static void Prefix(CharacterRopeHandling __instance) { __instance.maxRopeAngle = 150f; }
}

// PEAK reduces rope updates to once every 600 checks after 60 seconds.
// Keep its normal serializer running while a climber can move the rope.
[HarmonyPatch(typeof(RopeSyncer), "ShouldSendData")]
internal static class SyncOccupiedRope
{
    private static void Postfix(RopeSyncer __instance, ref bool __result, ref bool ___updateVisualizerManually)
    {
        Rope rope = __instance.rope;
        if (rope && rope.photonView.IsMine && !rope.creatorLeft && rope.isClimbable
            && rope.charactersClimbing.Count > 0 && rope.SegmentCount > 0)
        {
            ___updateVisualizerManually = false;
            __result = true;
        }
    }
}

public class RopeSwingInput : MonoBehaviourPun
{
    // Each climber has independent input, follow and release state. Rope physics
    // stays on its owner; character physics stays on the character's own client.
    private int velocityRopeId;
    private Vector3 ownerVelocity;
    private double ownerSampleTime = double.NegativeInfinity;
    private float ownerSampleReceived = float.NegativeInfinity;
    private float nextVelocitySend;
    private Photon.Realtime.Player velocitySender;

    internal static bool IsFiniteVelocity(Vector3 value) { return Finite(value); }

    private static bool Finite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    [PunRPC]
    public void RopeSwingVelocityRpc(int ropeId, Vector3 velocity, double sampleTime, PhotonMessageInfo info)
    {
        if (!character || !character.IsLocal || !character.data.isRopeClimbing
            || !Finite(velocity) || double.IsNaN(sampleTime) || double.IsInfinity(sampleTime)) return;
        Rope rope = character.data.heldRope;
        if (!rope || !rope.photonView || rope.photonView.ViewID != ropeId) return;
        var authority = rope.photonView.Owner ?? PhotonNetwork.MasterClient;
        if (info.Sender == null || info.Sender != authority) return;
        double age = PhotonNetwork.Time - sampleTime;
        if (age < -0.1 || age > 0.5) return;
        if (velocityRopeId == ropeId && velocitySender == info.Sender && sampleTime <= ownerSampleTime) return;
        velocityRopeId = ropeId;
        velocitySender = info.Sender;
        ownerVelocity = velocity;
        ownerSampleTime = sampleTime;
        ownerSampleReceived = Time.unscaledTime;
    }

    internal Vector3 GripVelocity(Rope rope, Vector3 fallback)
    {
        var authority = rope.photonView.Owner ?? PhotonNetwork.MasterClient;
        return velocityRopeId == rope.photonView.ViewID && velocitySender == authority
            && Time.unscaledTime - ownerSampleReceived <= 0.25f
            && PhotonNetwork.Time - ownerSampleTime <= 0.35
            ? ownerVelocity : fallback;
    }

    internal void SendRopeVelocity(Rope rope, float percent)
    {
        if (!rope.photonView.IsMine || !PhotonNetwork.InRoom || !photonView
            || photonView.IsMine || photonView.Owner == null || Time.unscaledTime < nextVelocitySend) return;
        Transform segment = rope.climbingAPI.GetSegmentFromPercent(percent);
        if (!segment || !segment.TryGetComponent(out Rigidbody body)) return;
        Vector3 target = rope.climbingAPI.GetPosition(percent) + character.data.ropeClimbNormal * 0.5f;
        Vector3 velocity = body.GetPointVelocity(target);
        if (!Finite(velocity)) return;
        // Target only this climber's owner. No broadcasts of character impulses.
        photonView.RPC(nameof(RopeSwingVelocityRpc), photonView.Owner,
            rope.photonView.ViewID, velocity, PhotonNetwork.Time);
        nextVelocitySend = Time.unscaledTime + 0.05f;
    }
    private Rope trackingRope;
    private Vector3 previousTarget, targetVelocity;

    internal Vector3 TrackVelocity(Rope rope, Vector3 target)
    {
        if (trackingRope != rope)
        {
            trackingRope = rope;
            previousTarget = target;
            targetVelocity = Vector3.zero;
            return targetVelocity;
        }
        Vector3 delta = target - previousTarget;
        // A teleport or remote resync is not a physical swing velocity.
        Vector3 measured = delta.sqrMagnitude > 9f ? Vector3.zero
            : Vector3.ClampMagnitude(delta / Time.fixedDeltaTime, 60f);
        previousTarget = target;
        targetVelocity = Vector3.Lerp(targetVelocity, measured, 1f - Mathf.Exp(-Time.fixedDeltaTime / 0.06f));
        return targetVelocity;
    }

    internal void ResetTracking()
    {
        trackingRope = null;
        targetVelocity = Vector3.zero;
        ownerSampleReceived = float.NegativeInfinity;
        followedRope = null;
        followedVelocity = Vector3.zero;
    }

    private Rope followedRope;
    private Vector3 followedVelocity;

    internal void Follow(Rope rope, Vector3 velocity)
    {
        followedRope = rope;
        followedVelocity = velocity;
    }

    internal void PrepareRelease(bool jumped)
    {
        if (jumped && character && character.IsLocal && followedRope
            && character.data.heldRope == followedRope && Finite(followedVelocity))
        {
            Vector3 velocity = GripVelocity(followedRope, followedVelocity);
            if (velocity.sqrMagnitude > 0.01f)
            {
                // Carry only missing speed in the rope's direction. Existing
                // momentum and PEAK's normal jump impulse are not overwritten.
                Vector3 direction = velocity.normalized;
                float missing = Mathf.Clamp(Vector3.Dot(velocity - character.data.avarageVelocity, direction), 0f, 8f);
                foreach (var part in character.refs.ragdoll.partList)
                    if (part.Rig && !part.Rig.isKinematic)
                        part.Rig.AddForce(direction * missing, ForceMode.VelocityChange);
            }
        }
        ResetTracking();
    }
    private void OnDisable() { ResetTracking(); }
    private void OnDestroy() { ResetTracking(); }
    private Character character;
    private Vector3 remoteInput;
    private Vector3 localInput;
    private Vector3 lastSentInput;
    private int remoteRopeId;
    private float remotePercent;
    private float lastReceived = float.NegativeInfinity;
    private float nextSend;
    private bool sentClimbing;
    private int lastSentRopeId;

    private void Awake() { character = GetComponent<Character>(); }

    // The RPC belongs to the existing character PhotonView, so no new network
    // objects, ownership requests, or per-segment PhotonViews are needed.
    [PunRPC]
    public void RopeSwingInputRpc(Vector3 direction, int ropeId, float percent, PhotonMessageInfo info)
    {
        if (!photonView || info.Sender == null || info.Sender != photonView.Owner)
            return;
        if (float.IsNaN(direction.x) || float.IsInfinity(direction.x)
            || float.IsNaN(direction.y) || float.IsInfinity(direction.y)
            || float.IsNaN(direction.z) || float.IsInfinity(direction.z))
            return;
        if (float.IsNaN(percent) || float.IsInfinity(percent))
            return;
        direction.y = 0f;
        remoteInput = Vector3.ClampMagnitude(direction, 1f);
        remoteRopeId = ropeId;
        remotePercent = Mathf.Clamp01(percent);
        lastReceived = Time.unscaledTime;
    }

    private void Update()
    {
        if (!character || character.data == null || !photonView)
            return;
        bool climbing = character.data.isRopeClimbing && character.data.heldRope;
        if (!climbing) ResetTracking();
        Vector3 input = Vector3.zero;
        if (character.IsLocal)
        {
            if (climbing)
            {
                float forward = (Input.GetKey(RopeSwing.Forward.Value) ? 1f : 0f)
                    - (Input.GetKey(RopeSwing.Back.Value) ? 1f : 0f);
                float side = (Input.GetKey(RopeSwing.Right.Value) ? 1f : 0f)
                    - (Input.GetKey(RopeSwing.Left.Value) ? 1f : 0f);
                Vector3 look = Vector3.ProjectOnPlane(character.data.lookDirection_Flat, Vector3.up).normalized;
                input = Vector3.ClampMagnitude(look * forward + Vector3.Cross(Vector3.up, look) * side, 1f);
            }

            int ropeId = climbing ? character.data.heldRope.photonView.ViewID : 0;
            // Send starts/reversals/releases immediately; refresh held input at 20 Hz.
            if (PhotonNetwork.InRoom && ((climbing && (Time.unscaledTime >= nextSend || ropeId != lastSentRopeId || (input - lastSentInput).sqrMagnitude > 0.04f)) || (!climbing && sentClimbing)))
            {
                photonView.RPC(nameof(RopeSwingInputRpc), RpcTarget.Others, input, ropeId,
                    climbing ? character.data.ropePercent : 0f);
                nextSend = Time.unscaledTime + 0.05f;
                sentClimbing = climbing;
                lastSentRopeId = ropeId;
                lastSentInput = input;
            }
            localInput = input;
        }
    }

    internal Vector3 GetInput(Rope rope, out float percent)
    {
        percent = 0f;
        if (!isActiveAndEnabled || !character || !character.data.isRopeClimbing || character.data.heldRope != rope)
            return Vector3.zero;
        if (character.IsLocal)
        {
            percent = Mathf.Clamp01(character.data.ropePercent);
            return localInput;
        }
        bool fresh = remoteRopeId == rope.photonView.ViewID && Time.unscaledTime - lastReceived <= 0.35f;
        percent = fresh ? remotePercent : Mathf.Clamp01(character.data.ropePercent);
        return fresh ? remoteInput : Vector3.zero;
    }
}

// One pass on the simulation owner combines inputs before applying forces.
// Clients never simulate the remote collider copies or steal rope ownership.
[HarmonyPatch(typeof(Rope), "FixedUpdate")]
internal static class SwingPhysics
{
    private static readonly Dictionary<Rigidbody, Vector3> Drive = new Dictionary<Rigidbody, Vector3>();
    private static readonly Dictionary<Rigidbody, float> ReachScale = new Dictionary<Rigidbody, float>();
    private static readonly Dictionary<Rigidbody, float> Loads = new Dictionary<Rigidbody, float>();
    private static readonly HashSet<Character> Occupants = new HashSet<Character>();

    private static void Postfix(Rope __instance)
    {
        Rope rope = __instance;
        if (!rope.photonView.IsMine || rope.creatorLeft || !rope.isClimbable || rope.antigrav
            || !rope.climbingAPI || rope.charactersClimbing.Count == 0)
            return;
        List<Transform> segments = rope.GetRopeSegments();
        if (segments.Count < 2)
            return;
        Drive.Clear();
        ReachScale.Clear();
        Loads.Clear();
        Occupants.Clear();
        foreach (Character climber in rope.charactersClimbing)
        {
            if (!climber || !Occupants.Add(climber) || !climber.TryGetComponent(out RopeSwingInput controls))
                continue;
            Vector3 input = controls.GetInput(rope, out float percent);
            // Occupancy and weight exist even when the player releases all keys.
            if (!climber.data.isRopeClimbing || climber.data.heldRope != rope) continue;
            controls.SendRopeVelocity(rope, percent);
            Transform held = rope.climbingAPI.GetSegmentFromPercent(percent);
            int index = segments.IndexOf(held);
            if (index < 0)
                continue;
            // Bounded weight assistance; keep the prefab mass ratios constant.
            for (int j = Mathf.Max(1, index - 1); j <= Mathf.Min(segments.Count - 1, index + 1); j++)
            {
                if (!segments[j].TryGetComponent(out Rigidbody loaded) || loaded.isKinematic) continue;
                Loads.TryGetValue(loaded, out float currentLoad);
                Loads[loaded] = Mathf.Min(3f, currentLoad + RopeSwing.ClimberLoad.Value * (j == index ? 1f : 0.5f));
            }
            if (input.sqrMagnitude < 0.0001f) continue;
            // Use distance along the upper rope, not world-space distance: a
            // folded rope must not lose drive just because it folds toward its anchor.
            float reach = 0f;
            for (int j = 1; j <= index; j++)
                reach += Vector3.Distance(segments[j - 1].position, segments[j].position);
            // Preserve short-rope tuning. Longer reaches get more leverage and
            // a higher speed allowance so successive pumps can build a wider arc.
            float reachScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, reach / 5f)), 1f, 2.5f);
            // Move the upper chain coherently, tapering drive near the anchor.
            // The tail is passive except for a short blend below the grip.
            for (int i = 1; i <= Mathf.Min(segments.Count - 1, index + 2); i++)
            {
                if (!segments[i].TryGetComponent(out Rigidbody body) || body.isKinematic)
                    continue;
                // The anchor-to-body axis defines the pendulum arc. The local
                // segment axis is a fallback when climbing close to the anchor.
                Vector3 radial = body.worldCenterOfMass - segments[0].position;
                if (radial.sqrMagnitude < 0.0625f)
                    radial = segments[i].up;
                Vector3 tangent = Vector3.ProjectOnPlane(input, radial.normalized);
                float weight = i <= index ? Mathf.Lerp(0.2f, 1f, (float)i / Mathf.Max(1, index))
                    : (i == index + 1 ? 0.35f : 0.15f);
                Drive.TryGetValue(body, out Vector3 accumulated);
                Drive[body] = accumulated + tangent * (weight * reachScale);
                ReachScale.TryGetValue(body, out float previousScale);
                ReachScale[body] = Mathf.Max(previousScale, reachScale);
            }
        }
        Vector3 anchorVelocity = segments[0].TryGetComponent(out Rigidbody anchorBody)
            ? anchorBody.linearVelocity : Vector3.zero;
        for (int i = 1; i < segments.Count; i++)
        {
            if (!segments[i].TryGetComponent(out Rigidbody body) || body.isKinematic)
                continue;
            Loads.TryGetValue(body, out float downwardLoad);
            if (downwardLoad > 0f) body.AddForce(Vector3.down * downwardLoad, ForceMode.Acceleration);
            Drive.TryGetValue(body, out Vector3 requested);
            // Cancellation happens before pumping and speed limiting, so equal
            // opposing inputs cancel. Limit cooperative boost to avoid runaway drive.
            ReachScale.TryGetValue(body, out float reachScale);
            reachScale = Mathf.Max(1f, reachScale);
            Vector3 drive = Vector3.ClampMagnitude(requested, 1.5f * reachScale);
            Vector3 radial = body.worldCenterOfMass - segments[0].position;
            if (radial.sqrMagnitude < 0.0625f)
                radial = segments[i].up;
            Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity - anchorVelocity, radial.normalized);
            Vector3 drag = -velocity * RopeSwing.CoastDamping.Value;
            if (drive.sqrMagnitude >= 0.0001f)
            {
                Vector3 sideways = Vector3.ProjectOnPlane(velocity, drive.normalized);
                drag -= sideways * RopeSwing.SteeringDamping.Value;
            }
            // Damping only removes motion; bounded acceleration avoids a heavy feel.
            drag = Vector3.ClampMagnitude(drag, 4f);
            if (drag.sqrMagnitude > 0.0001f)
                body.AddForce(drag, ForceMode.Acceleration);
            if (drive.sqrMagnitude < 0.0001f)
                continue;
            Vector3 direction = drive.normalized;
            float alongSpeed = Vector3.Dot(velocity, direction);
            float speed = RopeSwing.MaxSpeed.Value * reachScale;
            // Keep full response through the useful pumping range. The old
            // taper weakened every forward push, even far below the speed limit.
            float fade = Mathf.InverseLerp(speed * 0.65f, speed, Mathf.Max(0f, alongSpeed));
            float assistance = 1f - Mathf.SmoothStep(0f, 1f, fade);
            float pump = 1f + RopeSwing.PumpBoost.Value * Mathf.Clamp01(alongSpeed / (speed * 0.5f));
            body.WakeUp();
            body.AddForce(drive * (RopeSwing.Acceleration.Value * assistance * pump), ForceMode.Acceleration);
        }
    }
}

// One bounded spring-damper controller replaces the position-only spring.
[HarmonyPatch(typeof(CharacterRopeHandling), "ClimbForce")]
internal static class FollowMovingRope
{
    private static void Postfix(CharacterRopeHandling __instance, Character ___character, ref Vector3 __result)
    {
        Character character = ___character;
        if (!character || !character.IsLocal || !character.data.isRopeClimbing)
            return;
        Rope rope = character.data.heldRope;
        if (!rope || !rope.climbingAPI || rope.SegmentCount < 2
            || !character.TryGetComponent(out RopeSwingInput controls)) return;
        float percent = Mathf.Clamp01(character.data.ropePercent);
        Vector3 target = rope.climbingAPI.GetPosition(percent) + character.data.ropeClimbNormal * 0.5f;
        Vector3 targetVelocity = controls.TrackVelocity(rope, target);
        Transform segment = rope.climbingAPI.GetSegmentFromPercent(percent);
        if (rope.photonView.IsMine && segment && segment.TryGetComponent(out Rigidbody body))
            targetVelocity = body.GetPointVelocity(target);
        else
            targetVelocity = controls.GripVelocity(rope, targetVelocity);
        if (!RopeSwingInput.IsFiniteVelocity(targetVelocity)) targetVelocity = Vector3.zero;
        targetVelocity = Vector3.ClampMagnitude(targetVelocity, 60f);
        controls.Follow(rope, targetVelocity);
        foreach (var part in character.refs.ragdoll.partList)
        {
            if (part.partType.ToString() != "Torso" || !part.Rig) continue;
            float stiffness = Mathf.Clamp(__instance.climbForce, 36f, 225f);
            float damping = 2f * Mathf.Sqrt(stiffness);
            float step = Time.fixedDeltaTime;
            float denominator = 1f + damping * step + stiffness * step * step;
            Vector3 acceleration = ((target - part.Rig.position) * stiffness
                + (targetVelocity - part.Rig.linearVelocity) * damping - Physics.gravity) / denominator;
            __result = Vector3.ClampMagnitude(acceleration, 100f);
            return;
        }
        // If a torso is unavailable, retain the game's original climbing force.
    }
}

// Transfer missing carrier momentum before PEAK invokes the normal jump.
// Clear follow state on every release, including stamina loss.
[HarmonyPatch(typeof(CharacterRopeHandling), "StopRopeClimbingRpc")]
internal static class ReleaseSwingGrip
{
    private static void Prefix(Character ___character, bool jumped)
    {
        if (___character && ___character.TryGetComponent(out RopeSwingInput controls))
            controls.PrepareRelease(jumped);
    }
}