namespace Melodroid_3.Hearing;

/// <summary>
/// Psychoacoustic constants describing the limits and resolution of human hearing.
/// Values are typical for a healthy young adult; individual variation is significant.
/// </summary>
public static class HearingConstants
{
    // Audible frequency range.
    public const double MinAudibleFrequencyHz = 20.0;
    public const double MaxAudibleFrequencyHz = 20_000.0;

    // Just-Noticeable Differences (JND): smallest change a listener can reliably detect.
    /// <summary>Pitch JND for pure sine tones in the mid-frequency range (~0.6%).</summary>
    public const double PitchJndFractionSine = 0.006;

    /// <summary>
    /// Action-to-sound latency JND: maximum delay between a triggering action
    /// (e.g. key press) and the resulting sound before it feels detached.
    /// </summary>
    public const double ActionToSoundLatencyJndMs = 50.0;

    /// <summary>Amplitude JND: smallest loudness change detectable.</summary>
    public const double AmplitudeJndDb = 1.0;

    /// <summary>Interaural time-difference JND, the basis for horizontal sound localisation.</summary>
    public const double InterauralTimeJndMicroseconds = 20.0;

    /// <summary>
    /// Upper frequency at which auditory-nerve firing can phase-lock to individual cycles.    
    /// </summary>
    public const double PhaseLockingThresholdHz = 1000.0;
}
