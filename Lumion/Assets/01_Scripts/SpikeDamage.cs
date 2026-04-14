using UnityEngine;

public class SpikeDamage : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private bool usarTrigger = true;
    [SerializeField] private bool usarColision = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!usarTrigger)
            return;

        DanarPlayer(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!usarColision)
            return;

        DanarPlayer(collision.gameObject);
    }

    private void DanarPlayer(GameObject obj)
    {
        if (obj == null)
            return;

        PlayerController player = obj.GetComponentInParent<PlayerController>();
        if (player != null)
            player.RecibirDanio();
    }
}
