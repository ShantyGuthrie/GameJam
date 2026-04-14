using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb2D;
    private SpriteRenderer[] _sprites;

    [Header("Movimiento")]
    private float movimientoHorizontal = 0f;
    [SerializeField] private float velocidadDeMovimiento = 6f;
    private bool mirandoDerecha = true;

    [Header("Vida y dano")]
    [SerializeField] private int vidasMaximas = 10;
    [SerializeField] private float duracionRalentizacion = 0.75f;
    [SerializeField, Range(0.1f, 1f)] private float multiplicadorVelocidadAlRecibirDanio = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float multiplicadorSaltoAlRecibirDanio = 0.7f;
    [SerializeField] private float invulnerabilidadTrasDanio = 0.35f;
    private int _vidasRestantes;
    private float _tiempoFinRalentizacion;
    private float _tiempoFinInvulnerabilidad;
    private float _multiplicadorVelocidadExterno = 1f;
    private float _multiplicadorSaltoExterno = 1f;

    [Header("Salto")]
    [SerializeField] private float fuerzaDeSalto = 12f;
    [SerializeField, Tooltip("Solo para el salto desde suelo: ventana coyote tras pulsar antes de tocar suelo. El doble salto en el aire solo cuenta pulsaciones nuevas (no buffer).")]
    private float bufferSaltoSegundos = 0.12f;
    [SerializeField, Tooltip("Solo si el overlap del suelo no se corta nunca en el salto: tras estos segundos y con velocidad casi nula se fuerza fin del \"aire\" del Animator.")]
    private float tiempoRespaldoAterrizajeAnimator = 0.55f;
    [SerializeField, Tooltip("Saltos maximos por cada vez que tocas suelo (2 = doble salto).")]
    private int saltosPorAterrizaje = 2;
    [SerializeField, Tooltip("Para resetear saltos: se considera suelo cuando el overlap toca y no estas subiendo.")]
    private float umbralVelocidadVerticalSuelo = 0.05f;

    [Header("Detección de Suelo")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Animator Controller")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    private Animator _animator;

    private InputAction _accionSalto;
    private float _bufferSaltoRestante;
    private bool _sueloParaSaltoPrev;
    private bool _tieneEstadoSueloPrev;
    // Tras saltar: Animator en aire hasta despegar (sin overlap) o hasta aterrizar (respaldo si el overlap no se corta nunca).
    private bool _animatorEsperandoDespegarDelSuelo;
    private float _tiempoInicioSaltoAnimator;
    private int _saltosRestantes;

    private void Awake()
    {
        _accionSalto = new InputAction(type: InputActionType.Button);
        _accionSalto.AddBinding("<Keyboard>/space");
        _accionSalto.AddBinding("<Keyboard>/w");
        _accionSalto.AddBinding("<Keyboard>/upArrow");
    }

    private void OnEnable()
    {
        _accionSalto?.Enable();
    }

    private void OnDisable()
    {
        _accionSalto?.Disable();
    }

    private void OnDestroy()
    {
        _accionSalto?.Dispose();
    }

    private void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        _sprites = GetComponentsInChildren<SpriteRenderer>(true);

        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            _animator = gameObject.AddComponent<Animator>();

        if (animatorController != null)
            _animator.runtimeAnimatorController = animatorController;

        _saltosRestantes = saltosPorAterrizaje;
        _vidasRestantes = Mathf.Max(1, vidasMaximas);
        ActualizarOpacidadPorVida();
    }

    private void Update()
    {
        float h = 0f;
        bool contactoSuelo = EnSuelo();
        bool sueloParaSalto = contactoSuelo && rb2D.linearVelocity.y <= umbralVelocidadVerticalSuelo;
        bool acabaDeAterrizar = _tieneEstadoSueloPrev && sueloParaSalto && !_sueloParaSaltoPrev;
        if (acabaDeAterrizar)
        {
            _saltosRestantes = saltosPorAterrizaje;
        }

        _sueloParaSaltoPrev = sueloParaSalto;
        _tieneEstadoSueloPrev = true;

        bool pulsoSaltoEsteFrame = _accionSalto.WasPressedThisFrame();
        if (pulsoSaltoEsteFrame)
            _bufferSaltoRestante = bufferSaltoSegundos;
        else
            _bufferSaltoRestante -= Time.deltaTime;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                h = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                h = 1f;
        }

        bool saltoDesdeSuelo = _bufferSaltoRestante > 0f && sueloParaSalto && _saltosRestantes > 0;
        bool saltoEnAire = pulsoSaltoEsteFrame && !sueloParaSalto && _saltosRestantes > 0;
        if (saltoDesdeSuelo || saltoEnAire)
        {
            rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, FuerzaSaltoActual());
            _bufferSaltoRestante = 0f;
            _saltosRestantes--;
            _animatorEsperandoDespegarDelSuelo = true;
            _tiempoInicioSaltoAnimator = Time.time;
        }

        if (_animatorEsperandoDespegarDelSuelo)
        {
            // Despegue real: el ground check ya no toca suelo.
            if (!contactoSuelo)
                _animatorEsperandoDespegarDelSuelo = false;
            // Caso raro: overlap que no se corta nunca; no usar vy≈0 (eso es también el vértice del salto y acorta la animación).
            else if (Time.time - _tiempoInicioSaltoAnimator >= tiempoRespaldoAterrizajeAnimator
                     && Mathf.Abs(rb2D.linearVelocity.y) < 0.12f)
                _animatorEsperandoDespegarDelSuelo = false;
        }

        movimientoHorizontal = h * VelocidadActual();

        bool enSueloAnimator = contactoSuelo && !_animatorEsperandoDespegarDelSuelo;

        if (_animator != null)
        {
            _animator.SetFloat("Horizontal", Mathf.Abs(movimientoHorizontal));
            _animator.SetBool("enSuelo", enSueloAnimator);
        }
    }

    private void FixedUpdate()
    {
        rb2D.linearVelocity = new Vector2(movimientoHorizontal, rb2D.linearVelocity.y);

        if (movimientoHorizontal > 0 && !mirandoDerecha)
            Girar();
        else if (movimientoHorizontal < 0 && mirandoDerecha)
            Girar();
    }

    private bool EnSuelo()
    {
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void Girar()
    {
        mirandoDerecha = !mirandoDerecha;
        Vector3 escala = transform.localScale;
        escala.x *= -1;
        transform.localScale = escala;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    public void RecibirDanio()
    {
        if (Time.time < _tiempoFinInvulnerabilidad || _vidasRestantes <= 0)
            return;

        _vidasRestantes--;
        _tiempoFinInvulnerabilidad = Time.time + invulnerabilidadTrasDanio;
        _tiempoFinRalentizacion = Time.time + duracionRalentizacion;
        ActualizarOpacidadPorVida();

        if (_vidasRestantes <= 0)
            Morir();
    }

    private float VelocidadActual()
    {
        float velocidadBase = velocidadDeMovimiento * Mathf.Clamp(_multiplicadorVelocidadExterno, 0.1f, 1f);

        if (EstaHerido())
            return velocidadBase * multiplicadorVelocidadAlRecibirDanio;

        return velocidadBase;
    }

    private float FuerzaSaltoActual()
    {
        float saltoBase = fuerzaDeSalto * Mathf.Clamp(_multiplicadorSaltoExterno, 0.1f, 1f);

        if (EstaHerido())
            return saltoBase * multiplicadorSaltoAlRecibirDanio;

        return saltoBase;
    }

    private bool EstaHerido()
    {
        return _vidasRestantes < Mathf.Max(1, vidasMaximas);
    }

    private void ActualizarOpacidadPorVida()
    {
        if (_sprites == null || _sprites.Length == 0)
            return;

        float alpha = Mathf.Clamp01((float)_vidasRestantes / Mathf.Max(1, vidasMaximas));
        for (int i = 0; i < _sprites.Length; i++)
        {
            Color c = _sprites[i].color;
            c.a = alpha;
            _sprites[i].color = c;
        }
    }

    private void Morir()
    {
        Debug.Log("Player sin vidas: chau.");
        gameObject.SetActive(false);
    }

    public void AplicarDebuffBoss(float multiplicadorVelocidad, float multiplicadorSalto)
    {
        _multiplicadorVelocidadExterno = Mathf.Clamp(multiplicadorVelocidad, 0.1f, 1f);
        _multiplicadorSaltoExterno = Mathf.Clamp(multiplicadorSalto, 0.1f, 1f);
    }

    public void LimpiarDebuffBoss()
    {
        _multiplicadorVelocidadExterno = 1f;
        _multiplicadorSaltoExterno = 1f;
    }

    public void CurarVida(int cantidad = 1)
    {
        if (cantidad <= 0 || _vidasRestantes <= 0)
            return;

        _vidasRestantes = Mathf.Min(vidasMaximas, _vidasRestantes + cantidad);
        ActualizarOpacidadPorVida();
    }
}