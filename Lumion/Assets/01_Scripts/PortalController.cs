using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalController : MonoBehaviour
{
    private static string _escenaEsperadaTrasPortal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InicializarInterceptorPortal()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedGlobal;
        SceneManager.sceneLoaded += OnSceneLoadedGlobal;
        _escenaEsperadaTrasPortal = null;
    }

    private static void OnSceneLoadedGlobal(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrWhiteSpace(_escenaEsperadaTrasPortal))
            return;

        string actual = NormalizarNombreEscenaStatic(scene.name);
        string esperada = NormalizarNombreEscenaStatic(_escenaEsperadaTrasPortal);
        if (actual == esperada)
        {
            _escenaEsperadaTrasPortal = null;
            return;
        }

        // Si otro flujo manda a la escena de relato, fuerza continuar al nivel esperado.
        if (actual.Contains("relato"))
        {
            string destino = _escenaEsperadaTrasPortal;
            _escenaEsperadaTrasPortal = null;
            SceneManager.LoadScene(destino);
        }
    }

    [Header("Portal")]
    [SerializeField] private GameObject portalObject;
    [SerializeField] private Animator portalAnimator;
    [SerializeField] private string portalAppearAnimationStateName = "PortalAppear";
    [SerializeField] private bool ocultarAlInicio = true;

    [Header("Transicion de nivel")]
    [SerializeField] private string nombreEscenaNivel1 = "level1";
    [SerializeField] private string nombreEscenaNivel2 = "level2";
    [SerializeField] private string nombreEscenaNivel3 = "Nivel3";

    private bool yaSeMostro;
    private bool yaTransporto;

    private void Awake()
    {
        if (portalObject == null)
            portalObject = gameObject;

        if (portalAnimator == null && portalObject != null)
            portalAnimator = portalObject.GetComponent<Animator>();

        if (ocultarAlInicio && portalObject != null)
            portalObject.SetActive(false);
    }

    public void MostrarPortal()
    {
        if (yaSeMostro)
            return;

        yaSeMostro = true;

        if (portalObject != null)
            portalObject.SetActive(true);

        ReproducirAnimPortal();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (DebeTransportar(other.gameObject))
            TransportarANivel2();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (DebeTransportar(collision.gameObject))
            TransportarANivel2();
    }

    private void ReproducirAnimPortal()
    {
        if (portalAnimator == null)
            return;

        string[] estadosPosibles = { portalAppearAnimationStateName, "PortalAppear", "portal_appear", "openportal" };
        for (int i = 0; i < estadosPosibles.Length; i++)
        {
            int stateHash = Animator.StringToHash(estadosPosibles[i]);
            if (portalAnimator.HasState(0, stateHash))
            {
                portalAnimator.Play(stateHash);
                return;
            }
        }

        portalAnimator.SetBool("isOpen", true);
    }

    private bool DebeTransportar(GameObject obj)
    {
        if (!yaSeMostro || yaTransporto)
            return false;

        return obj.CompareTag("Player") || obj.GetComponentInParent<PlayerController>() != null;
    }

    private void TransportarANivel2()
    {
        string nombreEscenaActual = SceneManager.GetActiveScene().name;
        string actualNormalizada = NormalizarNombreEscena(nombreEscenaActual);

        if (actualNormalizada == "level1" || actualNormalizada == "nivel1" || CoincideEscena(nombreEscenaActual, nombreEscenaNivel1))
        {
            if (IntentarCargarEscenaDestino(nombreEscenaNivel2, "level2", "nivel2"))
            {
                yaTransporto = true;
                return;
            }
        }

        if (actualNormalizada == "level2" || actualNormalizada == "nivel2" || CoincideEscena(nombreEscenaActual, nombreEscenaNivel2))
        {
            if (IntentarCargarEscenaDestino(nombreEscenaNivel3, "nivel3", "level3"))
            {
                yaTransporto = true;
                return;
            }
        }

        if (actualNormalizada == "nivel3" || actualNormalizada == "level3" || CoincideEscena(nombreEscenaActual, nombreEscenaNivel3))
        {
            yaTransporto = true;
            FinalizarJuego();
            return;
        }

        Debug.LogError(
            $"No se pudo resolver la transicion de portal desde '{nombreEscenaActual}'. Configura '{nameof(nombreEscenaNivel1)}', '{nameof(nombreEscenaNivel2)}' y '{nameof(nombreEscenaNivel3)}'.",
            this);
        yaTransporto = false;
    }

    private bool CoincideEscena(string actual, string esperada)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(esperada))
            return false;

        return string.Equals(actual, esperada, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IntentarCargarEscenaDestino(string principal, params string[] aliases)
    {
        if (CargarPorNombreSiExiste(principal))
            return true;

        if (aliases != null)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                if (CargarPorNombreSiExiste(aliases[i]))
                    return true;
            }
        }

        int indice = BuscarIndiceEnBuild(principal, aliases);
        if (indice >= 0)
        {
            _escenaEsperadaTrasPortal = principal;
            PlayerController.NotificarCambioEscenaPorPortal();
            SceneManager.LoadScene(indice);
            return true;
        }

        return false;
    }

    private bool CargarPorNombreSiExiste(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        if (!Application.CanStreamedLevelBeLoaded(nombre))
            return false;

        _escenaEsperadaTrasPortal = nombre;
        PlayerController.NotificarCambioEscenaPorPortal();
        SceneManager.LoadScene(nombre);
        return true;
    }

    private int BuscarIndiceEnBuild(string principal, params string[] aliases)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string nombreBuild = System.IO.Path.GetFileNameWithoutExtension(path);
            string n = NormalizarNombreEscena(nombreBuild);
            if (n == NormalizarNombreEscena(principal))
                return i;

            if (aliases == null)
                continue;

            for (int j = 0; j < aliases.Length; j++)
            {
                if (n == NormalizarNombreEscena(aliases[j]))
                    return i;
            }
        }

        return -1;
    }

    private string NormalizarNombreEscena(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Empty;

        return nombre.Trim().ToLowerInvariant().Replace(" ", "");
    }

    private static string NormalizarNombreEscenaStatic(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Empty;

        return nombre.Trim().ToLowerInvariant().Replace(" ", "");
    }

    private void FinalizarJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
