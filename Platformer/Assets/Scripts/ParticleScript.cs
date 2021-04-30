using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleScript : MonoBehaviour
{
    public ParticleSystem _dust;

    private MovementScript _movementScript;


    void Start()
    {
        _movementScript = GetComponent<MovementScript>();
    }

    void Update()
    {
        if (_movementScript._playDust)
        {
            CreateDust();
            _movementScript._playDust = false;
        }
    }

    private void CreateDust()
    {
        _dust.Play();
    }
}
