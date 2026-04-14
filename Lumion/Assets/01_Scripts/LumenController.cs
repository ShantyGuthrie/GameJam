using UnityEngine;

public class LumenController : MonoBehaviour
{
    [Header("Curacion")]
    [SerializeField] private int vidaARecuperar = 10;
    [SerializeField] private bool destruirAlRecoger = true;

    [Header("Efecto respiracion")]
    [SerializeField] private bool usarRespiracion = true;
    [SerializeField] private float velocidadRespiracion = 2.2f;
    [SerializeField] private float intensidadEscala = 0.08f;
    [SerializeField] private bool respirarOpacidad = true;
    [SerializeField] private float opacidadMinima = 0.7f;

    private Vector3 _escalaBase;
    private SpriteRenderer[] _sprites;

    private void Awake()
    {
        _escalaBase = transform.localScale;
        _sprites = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        if (!usarRespiracion)
            return;

        float onda = Mathf.Sin(Time.time * Mathf.Max(0.01f, velocidadRespiracion));
        float factorEscala = 1f + onda * Mathf.Max(0f, intensidadEscala);
        transform.localScale = _escalaBase * factorEscala;

        if (!respirarOpacidad || _sprites == null)
            return;

        float alpha = Mathf.Lerp(Mathf.Clamp01(opacidadMinima), 1f, (onda + 1f) * 0.5f);
        for (int i = 0; i < _sprites.Length; i++)
        {
            Color c = _sprites[i].color;
            c.a = alpha;
            _sprites[i].color = c;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CurarPlayer(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CurarPlayer(collision.gameObject);
    }

    private void CurarPlayer(GameObject obj)
    {
        if (obj == null)
            return;

        PlayerController player = obj.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        player.CurarVida(Mathf.Max(1, vidaARecuperar));

        if (destruirAlRecoger)
            Destroy(gameObject);
    }
}
