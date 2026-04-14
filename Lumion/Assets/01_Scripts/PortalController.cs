using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalController : MonoBehaviour
{
    [Header("Portal")]
    [SerializeField] private GameObject portalObject;
    [SerializeField] private Animator portalAnimator;
    [SerializeField] private string portalAppearAnimationStateName = "PortalAppear";
    [SerializeField] private bool ocultarAlInicio = true;

    [Header("Transicion de nivel")]
    [SerializeField] private string nombreEscenaNivel2 = "Nivel2";
    [SerializeField] private int indiceEscenaNivel2 = 1;
    [SerializeField] private bool usarNombreEscena = true;

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
        if (usarNombreEscena && !string.IsNullOrWhiteSpace(nombreEscenaNivel2) &&
            Application.CanStreamedLevelBeLoaded(nombreEscenaNivel2))
        {
            yaTransporto = true;
            SceneManager.LoadScene(nombreEscenaNivel2);
            return;
        }

        if (indiceEscenaNivel2 >= 0 && indiceEscenaNivel2 < SceneManager.sceneCountInBuildSettings)
        {
            yaTransporto = true;
            SceneManager.LoadScene(indiceEscenaNivel2);
            return;
        }

        Debug.LogError(
            $"No se pudo cargar el nivel 2. Verifica Build Profiles y configura '{nameof(nombreEscenaNivel2)}' o '{nameof(indiceEscenaNivel2)}' correctamente.",
            this);
        yaTransporto = false;
    }
}
