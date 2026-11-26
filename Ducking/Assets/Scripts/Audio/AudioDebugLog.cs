using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.Audio
{
    /// <summary>
    /// Records what the mixer was told to do and what it actually outputs, so a
    /// sound that cannot be heard can be diagnosed instead of guessed at.
    ///
    /// A silent button has several possible causes that look identical from the
    /// player's chair: the clip is missing, the scene has no listener, a slider sits
    /// at zero, a source is muted by the mixer, or the sound plays far from the
    /// camera. The file this writes answers which one it is.
    /// Lives beside the AudioDirector so it can read every source it owns.
    /// </summary>
    [DefaultExecutionOrder(-49)]
    [RequireComponent(typeof(AudioDirector))]
    public class AudioDebugLog : MonoBehaviour
    {
        private const string LogPath = @"E:\Github-Duckkking\Ducking\UnityAudioLog.txt";
        private const float SampleInterval = 0.5f;
        private const int SampleLength = 1024;

        private AudioDirector _director;
        private float _timer;
        private readonly float[] _buffer = new float[SampleLength];

        private void Start()
        {
            _director = GetComponent<AudioDirector>();

            // Start each session with a clean file so old runs cannot be confused
            // with the current one.
            try { File.WriteAllText(LogPath, string.Empty); } catch { }

            Write($"SESSION scene={SceneManager.GetActiveScene().name} " +
                  $"master={AudioListener.volume:F2} " +
                  $"listeners={FindObjectsOfType<AudioListener>().Length} " +
                  $"musicVol={_director.MusicVolume:F2} sfxVol={_director.SfxVolume:F2} " +
                  $"muteAll={AudioListener.pause}");

            int buttons = 0;
            var found = FindObjectsOfType<Button>(true);
            for (int i = 0; i < found.Length; i++)
            {
                string label = found[i].name;
                found[i].onClick.AddListener(() => Write($"CLICK button={label}"));
                buttons++;
            }

            Write($"BUTTONS scene={SceneManager.GetActiveScene().name} count={buttons}");
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < SampleInterval) return;
            _timer = 0f;

            var sources = GetComponents<AudioSource>();
            int playing = 0;
            float peak = 0f;

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || !source.isPlaying) continue;

                playing++;
                source.GetOutputData(_buffer, 0);
                for (int k = 0; k < _buffer.Length; k += 8)
                {
                    float a = Mathf.Abs(_buffer[k]);
                    if (a > peak) peak = a;
                }
            }

            Write($"LIVE scene={SceneManager.GetActiveScene().name} sources={sources.Length} " +
                  $"playing={playing} peak={peak:F3} " +
                  $"musicVol={_director.MusicVolume:F2} sfxVol={_director.SfxVolume:F2}");
        }

        private static void Write(string line)
        {
            try
            {
                File.AppendAllText(LogPath, $"{Time.realtimeSinceStartup:F2}s {line}\n");
            }
            catch
            {
                // A failed log must never break the game.
            }
        }
    }
}