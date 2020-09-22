using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
	enum direction {
		Down = 0,
		Up = 1,
		Right = 2,
		Left = 3
	}

	enum animParams {
		Speed = 0,
		directionX = 1,
		directionY = 2
	}
	float speed;
	public GameObject playerGFX;
	Animator anim;

	void Start() {
		anim = playerGFX.GetComponent<Animator>();
	}

	public void move(InputAction.CallbackContext c) {
		Vector2 direction = c.ReadValue<Vector2>();
		anim.SetFloat("Speed", direction.sqrMagnitude);
		if (direction.sqrMagnitude == 0) {
			anim.SetTrigger("idle");
		} else {
			if(direction.x > 0) {
				anim.SetTrigger("runRight");
			} else if(direction.x < 0) {
				anim.SetTrigger("runLeft");
			} else if (direction.y > 0) {
				anim.SetTrigger("runUp");
			} else {
				anim.SetTrigger("runDown");
			}
		}
		Debug.Log(direction);
	}

	// Update is called once per frame
	void Update()
    {
		//controls.Player.Move
    }
}
