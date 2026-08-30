using UnityEngine;

public class MenuAudio : MonoBehaviour
{
    [SerializeField] private AudioSource m_AudioSource;
    public void PlayAudio()
    {
        m_AudioSource.Play();
    }
}
