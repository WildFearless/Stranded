using Sandbox;

public sealed class CameraMovement : Component
{
	[Property] public PlayerController Player { get; set; }
	[Property] public GameObject Body { get; set; }
	[Property] public GameObject Head { get; set; }
	[Property] public float Distance { get; set; }
	
	public bool IsFirstPerson => Distance == 0f; // Helpful but not required. You could always just check if Distance == 0f
	private CameraComponent _camera;
	private ModelRenderer _bodyRenderer;
	public Vector3 CurrentOffset = Vector3.Zero;

	protected override void OnStart()
	{
		_camera = Player.Components.GetInChildren<CameraComponent>();
		_bodyRenderer = Body.Components.Get<ModelRenderer>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;
		
		if ( Input.Pressed( "View" ) )
		{
			Distance = Distance == 0f ? 150f : 0f;
		}

		// Rotate the head based on mouse movement
		var eyeAngles = Head.Transform.Rotation.Angles();
		eyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		eyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		eyeAngles.roll = 0f;
		eyeAngles.pitch = eyeAngles.pitch.Clamp( -89.9f, 89.9f );

		Head.Transform.Rotation = Rotation.From( eyeAngles );
		
		// Set the current camera offset
		var targetOffset = Vector3.Zero;
		if ( Player.IsCrouching ) targetOffset += Vector3.Down * 32f;
		CurrentOffset = Vector3.Lerp(CurrentOffset, targetOffset, Time.Delta * 10f);
		
		// Set the position of the camera
		if ( _camera is not null )
		{
			var camPos = Head.Transform.Position + CurrentOffset;
			if ( !IsFirstPerson )
			{
				// Perform a trace backwards to see where we can safely place the camera
				var camForward = eyeAngles.ToRotation().Forward;
				var camTrace = Scene.Trace.Ray( camPos, camPos - (camForward * Distance) ).WithoutTags("player", "trigger").Run();

				if ( camTrace.Hit )
				{
					camPos = camTrace.HitPosition + camTrace.Normal;
				}
				else
				{
					camPos = camTrace.EndPosition;
				}
				
				// Show the body if not in first person
				_bodyRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
			}
			else
			{
				// Hide body if in first person
				_bodyRenderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
			}
			
			// Set the position of the camera to our calculated position
			_camera.Transform.Position = camPos;
			_camera.Transform.Rotation = eyeAngles.ToRotation();

		}
	}
}
