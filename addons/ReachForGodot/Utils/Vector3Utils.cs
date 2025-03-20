namespace ReaGE;

using Godot;

public static class Vector3Utils
{
    /// <summary>
    /// Converts a direction vector to a quaternion representing the required rotation from the given axis.
    /// </summary>
    public static Quaternion DirectionToQuaternion(this Vector3 direction, Vector3 axis)
    {
        var axisDiff = axis.Cross(direction);
        if (axisDiff.IsZeroApprox()) {
            return direction.Z < 0 ? Quaternion.Identity : Quaternion.FromEuler(new Vector3(0, Mathf.Pi, 0));
        } else {
            var ang = axis.AngleTo(direction);
            return new Quaternion(axisDiff.Normalized(), ang).Normalized();
        }
    }
}