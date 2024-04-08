using Sandbox;

public sealed class RockColliderScript : Component, Component.ITriggerListener
{
	private SoundEvent StartSound = new("sounds/tools/sfm/beep.vsnd_c") 
	{
		UI = true
	};
	
	private SoundEvent StopSound = new("sounds/tools/sfm/denyundo.vsnd_c") 
	{
		UI = true
	};
	
	private bool CanStartRocking;
	private bool IsWithinRockRange;
	private PlayerController Player { get; set; }
	
	public void OnTriggerEnter( Collider other )
	{
		if ( other is CapsuleCollider && other.Tags.Has( "player" ))
		{
			Player = other.Components.Get<PlayerController>();
			Log.Info("Rock on trigger");
			CanStartRocking = true;
			IsWithinRockRange = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( Player != null && IsWithinRockRange)
		{
			if ( Input.Pressed( "use" ) && CanStartRocking)
			{
				Sound.Play( StartSound );
				Player.IsRocking = true;
				CanStartRocking = false;
			}
			else if ( Input.Pressed( "use" ) && !CanStartRocking )
			{
				Sound.Play( StopSound );
				Player.IsRocking = false;
				CanStartRocking = true;
				Player.Timer = 0f;
			}
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( Player != null && IsWithinRockRange )
		{
			if(Player.IsRocking)
				Sound.Play( StopSound );
			
			Log.Info( "outside rock" );
			CanStartRocking = false;
			Player.IsRocking = false;
			Player.Timer = 0f;
			IsWithinRockRange = false;
		}
	}
}
