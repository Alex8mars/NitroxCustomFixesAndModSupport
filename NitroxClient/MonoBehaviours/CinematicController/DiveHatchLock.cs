using UnityEngine;

namespace NitroxClient.MonoBehaviours.CinematicController;

public class DiveHatchLock : MonoBehaviour
{
    private const float DEFAULT_LOCK_DURATION = 15f;

    private bool locked;
    private float unlockTime;

    public bool IsLocked => locked;

    public void Lock(float duration = DEFAULT_LOCK_DURATION)
    {
        locked = true;
        unlockTime = Time.time + duration;
    }

    public void Unlock()
    {
        locked = false;
    }

    private void Update()
    {
        if (locked && Time.time >= unlockTime)
        {
            Unlock();
        }
    }
}
