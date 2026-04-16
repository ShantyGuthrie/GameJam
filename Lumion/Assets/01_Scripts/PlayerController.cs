using System.Collections;
using System.IO;
using UnityEngine;using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    private static bool _cambioEscenaPorPortalEnCurso;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InicializarBloqueoCambioEscena()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedGlobal;
        SceneManager.sceneLoaded += OnSceneLoadedGlobal;
        _cambioEscenaPorPortalEnCurso = false;
    }

    private static void OnSceneLoadedGlobal(Scene scene, LoadSceneMode mode)
    {
        _cambioEscenaPorPortalEnCurso = false;
    }

    public static void NotificarCambioEscenaPorPortal()
    {
        _cambioEscenaPorPortalEnCurso = true;
    }

    public enum FuenteDanio
    {
        Generico = 0,
        Boss = 1,
        Pinchos = 2
    }

    private Rigidbody2D rb2D;
    private SpriteRenderer[] _sprites;

    [Header("Movimiento")]
    private float movimientoHorizontal = 0f;
    [SerializeField] private float velocidadDeMovimiento = 6f;
    private bool mirandoDerecha = true;

    [Header("Vida")]
    [SerializeField] private int vidasMaximas = 10;
    [SerializeField] private float invulnerabilidadTrasDanio = 0.5f;
    private int _vidasRestantes;
    private float _tiempoFinInvulnerabilidad;
    private float _multiplicadorVelocidadExterno = 1f;
    private float _multiplicadorSaltoExterno = 1f;

    [Header("Salto")]
    [SerializeField] private float fuerzaDeSalto = 12f;
    [SerializeField, Tooltip("Ventana de buffer para salto en suelo.")]
    private float bufferSaltoSegundos = 0.12f;
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
    [SerializeField] private float duracionOnDamageAnimator = 0.2f;
    private Animator _animator;
    private bool _animatorTieneOnDamage;

    [Header("Efecto Pinchos")]
    [SerializeField] private float duracionDistorsionPinchos = 0.35f;
    [SerializeField, Range(0f, 1f)] private float intensidadDistorsionPinchos = 0.18f;
    [SerializeField, Range(0f, 1f)] private float alphaMinimoGolpePinchos = 0.45f;
    [SerializeField] private float fuerzaRebotePinchos = 4.5f;

    [Header("Respiracion")]
    [SerializeField] private bool usarRespiracion = true;
    [SerializeField] private float velocidadRespiracion = 1.6f;
    [SerializeField] private float intensidadRespiracion = 0.08f;
    [SerializeField, Tooltip("Cuanto se ensancha/achica en X durante la respiracion.")]
    private float compresionHorizontalRespiracion = 0.45f;
    [SerializeField] private Transform objetivoRespiracion;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSourceSfx;
    [SerializeField] private AudioClip sonidoSalto;
    [SerializeField] private float duracionMaxSonidoSalto = 1f;

    [Header("Escena al morir")]
    [SerializeField] private string nombreEscenaInicio = "Inicio";
    [SerializeField] private int indiceEscenaInicio = 0;
    [SerializeField] private string[] nombresEscenaInicioAlternativos = { "MenuPrincipal", "MainMenu", "Inicio" };

    private InputAction _accionSalto;
    private float _bufferSaltoRestante;
    private int _saltosRestantes;
    private Vector3 _escalaBaseRespiracion;
    private float _tiempoFinOnDamageAnimator;
    private float _tiempoInicioDistorsionPinchos;
    private float _tiempoFinDistorsionPinchos;
    private float _alphaBasePorVida = 1f;
    private Coroutine _rutinaCorteSonidoSalto;
    private bool _estaMuriendo;

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

        _animatorTieneOnDamage = TieneParametroAnimator("OnDamage", AnimatorControllerParameterType.Bool);

        if (objetivoRespiracion == null && _animator != null && _animator.transform != transform)
            objetivoRespiracion = _animator.transform;
        if (objetivoRespiracion != null)
            _escalaBaseRespiracion = objetivoRespiracion.localScale;

        if (audioSourceSfx == null)
            audioSourceSfx = GetComponent<AudioSource>();
        if (audioSourceSfx == null)
            audioSourceSfx = gameObject.AddComponent<AudioSource>();
        audioSourceSfx.playOnAwake = false;
        audioSourceSfx.loop = false;
        audioSourceSfx.spatialBlend = 0f;

        _saltosRestantes = saltosPorAterrizaje;
        _vidasRestantes = Mathf.Max(1, vidasMaximas);
        ActualizarOpacidadPorVida();
    }

    private void Update()
    {
        float h = 0f;
        bool contactoSuelo = EnSuelo();
        bool sueloParaSalto = contactoSuelo && rb2D.linearVelocity.y <= umbralVelocidadVerticalSuelo;

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
            float salto = fuerzaDeSalto * Mathf.Clamp(_multiplicadorSaltoExterno, 0.1f, 1f);
            rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, salto);
            _bufferSaltoRestante = 0f;
            _saltosRestantes--;
            ReproducirSonidoSaltoCortado();
        }

        movimientoHorizontal = h * velocidadDeMovimiento * Mathf.Clamp(_multiplicadorVelocidadExterno, 0.1f, 1f);

        if (_animator != null)
        {
            _animator.SetFloat("Horizontal", Mathf.Abs(movimientoHorizontal));
            _animator.SetBool("enSuelo", sueloParaSalto);
            if (_animatorTieneOnDamage)
                _animator.SetBool("OnDamage", Time.time < _tiempoFinOnDamageAnimator);
        }

        ActualizarRespiracion();
        ActualizarVisualGolpePinchos();
    }

    private void FixedUpdate()
    {
        rb2D.linearVelocity = new Vector2(movimientoHorizontal, rb2D.linearVelocity.y);

        if (movimientoHorizontal > 0 && !mirandoDerecha)
            Girar();
        else if (movimientoHorizontal < 0 && mirandoDerecha)
            Girar();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!EsSueloDeAterrizaje(collision))
            return;

        // Reset de saltos solo al aterrizar por colision real.
        _saltosRestantes = saltosPorAterrizaje;
    }

    private bool EnSuelo()
    {
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool EsSueloDeAterrizaje(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return false;

        if ((groundLayer.value & (1 << collision.collider.gameObject.layer)) == 0)
            return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contacto = collision.GetContact(i);
            if (contacto.normal.y > 0.25f)
                return true;
        }

        return false;
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

    /// <returns>true si resto vida (no bloqueado por invulnerabilidad).</returns>
    public bool RecibirDanio(FuenteDanio fuenteDanio = FuenteDanio.Generico)
    {
        if (_vidasRestantes <= 0)
            return false;

        if (Time.time < _tiempoFinInvulnerabilidad)
            return false;

        _vidasRestantes--;
        _tiempoFinInvulnerabilidad = Time.time + Mathf.Max(0.05f, invulnerabilidadTrasDanio);
        if (fuenteDanio == FuenteDanio.Pinchos)
        {
            ActivarOnDamageAnimator();
            ActivarDistorsionPinchos();
            AplicarRebotePinchos();
        }
        ActualizarOpacidadPorVida();

        if (_vidasRestantes <= 0)
            Morir();

        return true;
    }

    public void CurarVida(int cantidad = 1)
    {
        if (_vidasRestantes <= 0 || cantidad <= 0)
            return;

        _vidasRestantes = Mathf.Min(vidasMaximas, _vidasRestantes + cantidad);
        LimpiarDebuffBoss();
        ActualizarOpacidadPorVida();
    }

    private void ActualizarOpacidadPorVida()
    {
        if (_sprites == null || _sprites.Length == 0)
            return;

        _alphaBasePorVida = Mathf.Clamp01((float)_vidasRestantes / Mathf.Max(1, vidasMaximas));
        AplicarAlphaSprites(_alphaBasePorVida);
    }

    private void AplicarAlphaSprites(float alpha)
    {
        if (_sprites == null || _sprites.Length == 0)
            return;

        float alphaClamped = Mathf.Clamp01(alpha);
        for (int i = 0; i < _sprites.Length; i++)
        {
            Color c = _sprites[i].color;
            c.a = alphaClamped;
            _sprites[i].color = c;
        }
    }

    private void Morir()
    {
        if (_cambioEscenaPorPortalEnCurso)
            return;

        if (_estaMuriendo)
            return;

        _estaMuriendo = true;
        StartCoroutine(CargarMenuPrincipalTrasMuerte());
    }

    private IEnumerator CargarMenuPrincipalTrasMuerte()
    {
        // Evita quedarse "congelado" si alguna escena dejo el juego pausado.
        Time.timeScale = 1f;
        yield return new WaitForEndOfFrame();

        if (IntentarCargarPorNombre("menuInicial"))
            yield break;

        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            SceneManager.LoadScene(0);
            yield break;
        }

        if (IntentarCargarPorNombre(nombreEscenaInicio))
            yield break;

        if (IntentarCargarNombresAlternativos())
            yield break;

        int indiceDetectado = BuscarIndiceMenuEnBuild();
        if (indiceDetectado >= 0)
        {
            SceneManager.LoadScene(indiceDetectado);
            yield break;
        }

        if (indiceEscenaInicio >= 0 && indiceEscenaInicio < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(indiceEscenaInicio);
            yield break;
        }

        Debug.LogError(
            $"No se pudo cargar la escena de inicio al morir. Configura '{nameof(nombreEscenaInicio)}', '{nameof(nombresEscenaInicioAlternativos)}' o '{nameof(indiceEscenaInicio)}' correctamente.",
            this);
    }

    private bool IntentarCargarPorNombre(string nombreEscena)
    {
        if (!string.IsNullOrWhiteSpace(nombreEscena) &&
            Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            SceneManager.LoadScene(nombreEscena);
            return true;
        }

        return false;
    }

    private bool IntentarCargarNombresAlternativos()
    {
        if (nombresEscenaInicioAlternativos == null || nombresEscenaInicioAlternativos.Length == 0)
            return false;

        for (int i = 0; i < nombresEscenaInicioAlternativos.Length; i++)
        {
            if (IntentarCargarPorNombre(nombresEscenaInicioAlternativos[i]))
                return true;
        }

        return false;
    }

    private int BuscarIndiceMenuEnBuild()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string ruta = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(ruta))
                continue;

            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (string.IsNullOrWhiteSpace(nombre))
                continue;

            string nombreLower = nombre.ToLowerInvariant();
            if (nombreLower.Contains("menu") || nombreLower.Contains("inicio"))
                return i;
        }

        return -1;
    }

    private void ActualizarRespiracion()
    {
        if (!usarRespiracion || objetivoRespiracion == null)
            return;

        float ondaBase = Mathf.Sin(Time.time * Mathf.Max(0.01f, velocidadRespiracion));
        // Curva mas organica: inhalacion suave y exhalacion un poco mas larga.
        float ondaRespiracion = Mathf.Sign(ondaBase) * Mathf.Pow(Mathf.Abs(ondaBase), 1.6f);
        float intensidad = Mathf.Max(0f, intensidadRespiracion);
        float factorY = 1f + (ondaRespiracion * intensidad);
        float factorX = 1f - (ondaRespiracion * intensidad * Mathf.Clamp01(compresionHorizontalRespiracion));

        Vector3 escala = _escalaBaseRespiracion;
        float signoActualX = Mathf.Sign(objetivoRespiracion.localScale.x);
        if (Mathf.Approximately(signoActualX, 0f))
            signoActualX = Mathf.Sign(_escalaBaseRespiracion.x);
        if (Mathf.Approximately(signoActualX, 0f))
            signoActualX = 1f;

        // Mantiene la direccion de mirada (izq/der) y solo respira en magnitud.
        escala.x = Mathf.Abs(_escalaBaseRespiracion.x) * factorX * signoActualX;
        escala.y *= factorY;

        // Distorsion breve cuando recibe dano de pinchos.
        float intensidadGolpe = IntensidadGolpePinchosActual();
        if (intensidadGolpe > 0f)
        {
            float wobble = Mathf.Sin((1f - intensidadGolpe) * 22f) * intensidadGolpe;
            float amp = Mathf.Clamp01(intensidadDistorsionPinchos);
            escala.x *= 1f + (wobble * amp);
            escala.y *= 1f - (wobble * amp * 0.9f);
        }

        objetivoRespiracion.localScale = escala;
    }

    private void ActivarOnDamageAnimator()
    {
        _tiempoFinOnDamageAnimator = Time.time + Mathf.Max(0.05f, duracionOnDamageAnimator);
    }

    private void ActivarDistorsionPinchos()
    {
        _tiempoInicioDistorsionPinchos = Time.time;
        _tiempoFinDistorsionPinchos = Time.time + Mathf.Max(0.05f, duracionDistorsionPinchos);
    }

    private void AplicarRebotePinchos()
    {
        if (rb2D == null)
            return;

        float rebote = Mathf.Max(0f, fuerzaRebotePinchos);
        float nuevaY = Mathf.Max(rb2D.linearVelocity.y, rebote);
        rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, nuevaY);
    }

    private float IntensidadGolpePinchosActual()
    {
        if (Time.time >= _tiempoFinDistorsionPinchos)
            return 0f;

        float duracion = Mathf.Max(0.001f, _tiempoFinDistorsionPinchos - _tiempoInicioDistorsionPinchos);
        float progreso = Mathf.Clamp01((Time.time - _tiempoInicioDistorsionPinchos) / duracion);
        return 1f - progreso;
    }

    private void ActualizarVisualGolpePinchos()
    {
        float intensidadGolpe = IntensidadGolpePinchosActual();
        if (intensidadGolpe <= 0f)
        {
            AplicarAlphaSprites(_alphaBasePorVida);
            return;
        }

        float progreso = 1f - intensidadGolpe;
        float pulso = Mathf.Sin(progreso * Mathf.PI);
        float alphaObjetivo = Mathf.Lerp(_alphaBasePorVida, _alphaBasePorVida * Mathf.Clamp01(alphaMinimoGolpePinchos), pulso);
        AplicarAlphaSprites(alphaObjetivo);
    }

    private bool TieneParametroAnimator(string nombre, AnimatorControllerParameterType tipo)
    {
        if (_animator == null)
            return false;

        AnimatorControllerParameter[] parametros = _animator.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            if (parametros[i].name == nombre && parametros[i].type == tipo)
                return true;
        }

        return false;
    }

    private void ReproducirSonidoSaltoCortado()
    {
        if (audioSourceSfx == null || sonidoSalto == null)
            return;

        if (_rutinaCorteSonidoSalto != null)
            StopCoroutine(_rutinaCorteSonidoSalto);

        audioSourceSfx.Stop();
        audioSourceSfx.clip = sonidoSalto;
        audioSourceSfx.time = 0f;
        audioSourceSfx.Play();
        _rutinaCorteSonidoSalto = StartCoroutine(CortarSonidoTrasTiempo(Mathf.Max(0.05f, duracionMaxSonidoSalto)));
    }

    private IEnumerator CortarSonidoTrasTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);

        if (audioSourceSfx != null && audioSourceSfx.isPlaying)
            audioSourceSfx.Stop();

        _rutinaCorteSonidoSalto = null;
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
}