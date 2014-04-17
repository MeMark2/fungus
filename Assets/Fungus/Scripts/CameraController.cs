using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;

namespace Fungus
{
	/**
	 * Controller for main camera.
	 * Supports several types of camera transition including snap, pan & fade.
	 */
	public class CameraController : MonoBehaviour 
	{
		Game game;

		// Manual panning control
		View manualPanViewA;
		View manualPanViewB;
		Vector3 previousMousePos;

		class CameraView
		{
			public Vector3 cameraPos;
			public float cameraSize;
		};

		Dictionary<string, CameraView> storedViews = new Dictionary<string, CameraView>();

		void Start()
		{
			game = Game.GetInstance();
		}

		public void Fade(float targetAlpha, float fadeDuration, Action fadeAction)
		{
			StartCoroutine(FadeInternal(targetAlpha, fadeDuration, fadeAction));
		}

		public void FadeToView(View view, float fadeDuration, Action fadeAction)
		{
			game.manualPanActive = false;

			// Fade out
			Fade(0f, fadeDuration / 2f, delegate {
				
				// Snap to new view
				PanToPosition(view.transform.position, view.viewSize, 0f, null);

				// Fade in
				Fade(1f, fadeDuration / 2f, delegate {
					if (fadeAction != null)
					{
						fadeAction();
					}
				});
			});
		}

		IEnumerator FadeInternal(float targetAlpha, float fadeDuration, Action fadeAction)
		{
			float startAlpha = Game.GetInstance().fadeAlpha;
			float timer = 0;

			while (timer < fadeDuration)
			{
				float t = timer / fadeDuration;
				timer += Time.deltaTime;

				t = Mathf.Clamp01(t);   

				Game.GetInstance().fadeAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
				yield return null;
			}

			Game.GetInstance().fadeAlpha = targetAlpha;

			if (fadeAction != null)
			{
				fadeAction();
			}
		}

		/**
		 * Positions camera so sprite is centered and fills the screen.
		 * @param spriteRenderer The sprite to center the camera on
		 */
		public void CenterOnSprite(SpriteRenderer spriteRenderer)
		{
			game.manualPanActive = false;

			Sprite sprite = spriteRenderer.sprite;
			Vector3 extents = sprite.bounds.extents;
			float localScaleY = spriteRenderer.transform.localScale.y;
			Camera.main.orthographicSize = extents.y * localScaleY;
			
			Vector3 pos = spriteRenderer.transform.position;
			Camera.main.transform.position = new Vector3(pos.x, pos.y, 0);
			SetCameraZ();
		}

		/**
		 * Moves camera from current position to a target position over a period of time.
		 */
		public void PanToPosition(Vector3 targetPosition, float targetSize, float duration, Action arriveAction)
		{
			game.manualPanActive = false;
			
			if (duration == 0f)
			{
				// Move immediately
				Camera.main.orthographicSize = targetSize;
				Camera.main.transform.position = targetPosition;
				SetCameraZ();
				if (arriveAction != null)
				{
					arriveAction();
				}
			}
			else
			{
				StartCoroutine(PanInternal(targetPosition, targetSize, duration, arriveAction));
			}
		}

		/**
		 * Stores the current camera view using a name.
		 */
		public void StoreView(string viewName)
		{
			CameraView currentView = new CameraView();
			currentView.cameraPos = Camera.main.transform.position;
			currentView.cameraSize = Camera.main.orthographicSize;
			storedViews[viewName] = currentView;
		}

		/**
		 * Moves the camera to a previously stored camera view over a period of time.
		 */
		public void PanToStoredView(string viewName, float duration, Action arriveAction)
		{
			if (!storedViews.ContainsKey(viewName))
			{
				// View has not previously been stored
				if (arriveAction != null)
				{
					arriveAction();
				}
				return;
			}

			CameraView cameraView = storedViews[viewName];

			if (duration == 0f)
			{
				// Move immediately
				Camera.main.transform.position = cameraView.cameraPos;
				Camera.main.orthographicSize = cameraView.cameraSize;
				SetCameraZ();
				if (arriveAction != null)
				{
					arriveAction();
				}
			}
			else
			{
				StartCoroutine(PanInternal(cameraView.cameraPos, cameraView.cameraSize, duration, arriveAction));
			}
		}
		
		IEnumerator PanInternal(Vector3 targetPos, float targetSize, float duration, Action arriveAction)
		{
			float timer = 0;
			float startSize = Camera.main.orthographicSize;
			float endSize = targetSize;
			Vector3 startPos = Camera.main.transform.position;
			Vector3 endPos = targetPos;

			bool arrived = false;
			while (!arrived)
			{
				timer += Time.deltaTime;
				if (timer > duration)
				{
					arrived = true;
					timer = duration;
				}

				// Apply smoothed lerp to camera position and orthographic size
				float t = timer / duration;
				Camera.main.orthographicSize = Mathf.Lerp(startSize, endSize, Mathf.SmoothStep(0f, 1f, t));
				Camera.main.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
				SetCameraZ();

				if (arrived &&
				    arriveAction != null)
				{
					arriveAction();
				}

				yield return null;
			}
		}

		/**
		 * Moves camera smoothly through a sequence of Views over a period of time
		 */
		public void PanToPath(View[] viewList, float duration, Action arriveAction)
		{
			game.manualPanActive = false;

			List<Vector3> pathList = new List<Vector3>();

			// Add current camera position as first point in path
			// Note: We use the z coord to tween the camera orthographic size
			Vector3 startPos = new Vector3(Camera.main.transform.position.x,
			                               Camera.main.transform.position.y,
			                               Camera.main.orthographicSize);
			pathList.Add(startPos);

			for (int i = 0; i < viewList.Length; ++i)
			{
				View view = viewList[i];

				Vector3 viewPos = new Vector3(view.transform.position.x, 
				                              view.transform.position.y, 
				                              view.viewSize);
				pathList.Add(viewPos);
			}

			StartCoroutine(PanToPathInternal(duration, arriveAction, pathList.ToArray()));
		}

		IEnumerator PanToPathInternal(float duration, Action arriveAction, Vector3[] path)
		{
			float timer = 0;

			while (timer < duration)
			{
				timer += Time.deltaTime;
				timer = Mathf.Min(timer, duration);
				float percent = timer / duration;

				Vector3 point = iTween.PointOnPath(path, percent);

				Camera.main.transform.position = new Vector3(point.x, point.y, 0);
				Camera.main.orthographicSize = point.z;
				SetCameraZ();

				yield return null;
			}

			if (arriveAction != null)
			{
				arriveAction();
			}
		}

		/**
		 * Activates manual panning mode.
		 * The player can pan the camera within the area between viewA & viewB.
		 */
		public void StartManualPan(View viewA, View viewB, float duration, Action arriveAction)
		{
			manualPanViewA = viewA;
			manualPanViewB = viewB;

			Vector3 cameraPos = Camera.main.transform.position;

			Vector3 targetPosition = CalcCameraPosition(cameraPos, manualPanViewA, manualPanViewB);
			float targetSize = CalcCameraSize(cameraPos, manualPanViewA, manualPanViewB); 

			PanToPosition(targetPosition, targetSize, duration, delegate {

				game.manualPanActive = true;

				if (arriveAction != null)
				{
					arriveAction();
				}
			}); 
		}

		/**
		 * Deactivates manual panning mode.
		 */
		public void StopManualPan()
		{
			game.manualPanActive = false;
			manualPanViewA = null;
			manualPanViewB = null;
		}

		/**
		 * Returns the current position of the main camera.
		 */
		public Vector3 GetCameraPosition()
		{
			return Camera.main.transform.position;
		}

		void SetCameraZ()
		{
			Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, game.cameraZ);
		}

		void Update()	
		{
			if (!game.manualPanActive)
			{
				return;
			}

			Vector3 delta = Vector3.zero;

			if (Input.touchCount > 0)
			{
				if (Input.GetTouch(0).phase == TouchPhase.Moved)
				{
					delta = Input.GetTouch(0).deltaPosition;
				}
			}

			if (Input.GetMouseButtonDown(0))
			{
				previousMousePos = Input.mousePosition;	
			}
			else if (Input.GetMouseButton(0)) 
			{
				delta = Input.mousePosition - previousMousePos;
				previousMousePos = Input.mousePosition;
			}

			Vector3 cameraDelta = Camera.main.ScreenToViewportPoint(delta);
			cameraDelta.x *= -2f;
			cameraDelta.y *= -1f;
			cameraDelta.z = 0f;

			Vector3 cameraPos = Camera.main.transform.position;

			cameraPos += cameraDelta;

			Camera.main.transform.position = CalcCameraPosition(cameraPos, manualPanViewA, manualPanViewB);
			Camera.main.orthographicSize = CalcCameraSize(cameraPos, manualPanViewA, manualPanViewB); 
		}

		// Clamp camera position to region defined by the two views
		Vector3 CalcCameraPosition(Vector3 pos, View viewA, View viewB)
		{
			Vector3 safePos = pos;

			// Clamp camera position to region defined by the two views
			safePos.x = Mathf.Max(safePos.x, Mathf.Min(viewA.transform.position.x, viewB.transform.position.x));
			safePos.x = Mathf.Min(safePos.x, Mathf.Max(viewA.transform.position.x, viewB.transform.position.x));
			safePos.y = Mathf.Max(safePos.y, Mathf.Min(viewA.transform.position.y, viewB.transform.position.y));
			safePos.y = Mathf.Min(safePos.y, Mathf.Max(viewA.transform.position.y, viewB.transform.position.y));

			return safePos;
		}

		// Smoothly interpolate camera orthographic size based on relative position to two views
		float CalcCameraSize(Vector3 pos, View viewA, View viewB)
		{
			// Get ray and point in same space
			Vector3 toViewB = viewB.transform.position - viewA.transform.position;
			Vector3 localPos = pos - viewA.transform.position;
			
			// Normalize
			float distance = toViewB.magnitude;
			toViewB /= distance;
			localPos /= distance;
			
			// Project point onto ray
			float t = Vector3.Dot(toViewB, localPos);
			t = Mathf.Clamp01(t); // Not really necessary but no harm
			
			float cameraSize = Mathf.Lerp(viewA.viewSize, viewB.viewSize, t);

			return cameraSize;
		}
	}
}
