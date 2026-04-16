using UnityEngine;
using UnityEngine.SceneManagement;

public class BackgroundMusicManager : MonoBehaviour
{
    private const string SceneLevel1 = "level1";
    private const string SceneLevel2 = "level2";
    private const string SceneLevel3 = "Nivel3";

    private static BackgroundMusicManager _instance;

    [Header("Musica por nivel")]
    [SerializeField] private AudioClip clipLevel1;
    [SerializeField] private AudioClip clipLevel2;
    [SerializeField] private AudioClip clipLevel3;  

    [Header("Ajustes")]
    [SerializeField, Range(0f, 1f)] private float volumen = 0.5f;
    [SerializeField] private bool usarFallbackResources = true;

    [Header("Fallback Resources (sin extension)")]
    [SerializeField] private string rutaLevel1Resources = "Audio/level1_bgm";
    [SerializeField] private string rutaLevel2Resources = "Audio/level2_bgm";
    [SerializeField] private string rutaLevel3Resources = "Audio/nivel3_bgm";

    private AudioSource _bgmSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("BackgroundMusicManager");
        _instance = go.AddComponent<BackgroundMusicManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.GetComponent<AudioSource>();
        if (_bgmSource == null)
            _bgmSource = gameObject.AddComponent<AudioSource>();

        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume = Mathf.Clamp01(volumen);

        CargarFallbackSiHaceFalta();
        ActualizarMusicaSegunEscena(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ActualizarMusicaSegunEscena(scene);
    }

    private void ActualizarMusicaSegunEscena(Scene scene)
    {
        AudioClip clipObjetivo = ObtenerClipParaEscena(scene.name);
        if (clipObjetivo == null)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            return;
        }

        if (_bgmSource.clip == clipObjetivo && _bgmSource.isPlaying)
            return;

        _bgmSource.Stop();
        _bgmSource.clip = clipObjetivo;
        _bgmSource.Play();
    }

    private AudioClip ObtenerClipParaEscena(string nombreEscena)
    {
        if (nombreEscena == SceneLevel1)
            return clipLevel1;

        if (nombreEscena == SceneLevel2)
            return clipLevel2;

        if (nombreEscena == SceneLevel3)
            return clipLevel3;

        return null;
    }

    private void CargarFallbackSiHaceFalta()
    {
        if (!usarFallbackResources)
            return;

        if (clipLevel1 == null && !string.IsNullOrWhiteSpace(rutaLevel1Resources))
            clipLevel1 = Resources.Load<AudioClip>(rutaLevel1Resources);

        if (clipLevel2 == null && !string.IsNullOrWhiteSpace(rutaLevel2Resources))
            clipLevel2 = Resources.Load<AudioClip>(rutaLevel2Resources);

        if (clipLevel3 == null && !string.IsNullOrWhiteSpace(rutaLevel3Resources))
            clipLevel3 = Resources.Load<AudioClip>(rutaLevel3Resources);
    }
}
