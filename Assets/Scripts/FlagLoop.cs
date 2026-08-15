using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

[RequireComponent(typeof(AlembicStreamPlayer))]
public class AlembicAutoPlay : MonoBehaviour
{
    [SerializeField] private float playbackSpeed = 1f;

    private AlembicStreamPlayer alembic;

    private void Awake()
    {
        alembic = GetComponent<AlembicStreamPlayer>();
    }

    private void Update()
    {
        if (alembic.Duration <= 0f)
            return;

        alembic.CurrentTime += Time.deltaTime * playbackSpeed;

        if (alembic.CurrentTime >= alembic.Duration)
        {
            alembic.CurrentTime %= alembic.Duration;
        }
    }
}