using System;

[Serializable]
public class HapticFeedbackPayload
{
    public float damageDealt;
    public float intensityRatio; // 0.0 to 1.0 based on damage ratio (or fixed value for fire events)

    // NEW: lets the receiving phone distinguish "you got hit" from "your shot
    // landed" without inferring it from damageDealt == 0. Flag this back to
    // me if you'd rather keep the payload exactly as originally given.
    public bool isDamageEvent;
}