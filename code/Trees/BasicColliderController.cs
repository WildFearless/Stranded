using Sandbox;

public sealed class BasicColliderController : Component, Component.ITriggerListener
{
	private SoundEvent StartSound = new("sounds/tools/sfm/beep.vsnd_c") 
	{
		UI = true
	};
	
	private SoundEvent StopSound = new("sounds/tools/sfm/denyundo.vsnd_c") 
	{
		UI = true
	};
	
	private bool CanStartCutting;
	private bool IsWithinTreeRange;
	private PlayerController Player { get; set; }
	
	public void OnTriggerEnter( Collider other )
	{
		if ( other is CapsuleCollider && other.Tags.Has( "player" ))
		{
			Player = other.Components.Get<PlayerController>();
			Log.Info("Tree on trigger");
			CanStartCutting = true;
			IsWithinTreeRange = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( Player != null && IsWithinTreeRange)
		{
			if ( Input.Pressed( "use" ) && CanStartCutting)
			{
				Sound.Play( StartSound );
				Player.IsCutting = true;
				CanStartCutting = false;
			}
			else if ( Input.Pressed( "use" ) && !CanStartCutting )
			{
				Sound.Play( StopSound );
				Player.IsCutting = false;
				CanStartCutting = true;
				Player.Timer = 0f;
			}
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( Player != null && IsWithinTreeRange )
		{
			if(Player.IsCutting)
				Sound.Play( StopSound );
			
			Log.Info( "outside tree" );
			CanStartCutting = false;
			Player.IsCutting = false;
			Player.Timer = 0f;
			IsWithinTreeRange = false;
		}
	}
}
