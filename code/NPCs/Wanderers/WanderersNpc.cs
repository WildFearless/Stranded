using Sandbox;
using Sandbox.NPCs;
using System.Threading.Tasks;

public sealed class WanderersNpc : Component, IBaseNpc
{
	private GameObject WandererPrefab = GameObject.GetPrefab("prefabs/wandererprefab.prefab");
	private Vector3 SpawnPosition;
	public int Health { get; set; } = 150;
	private bool _isDead;
	private SkinnedModelRenderer _bodyRenderer;

	protected override void OnStart()
	{
		SpawnPosition = WorldPosition;
		_bodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants );
		Log.Info( $"WanderersNpc.OnStart: {GameObject.Name} renderer found = {_bodyRenderer != null}" );
	}

	protected override void OnUpdate()
	{
		if ( Health <= 0 && !_isDead )
		{
			OnDeath();
		}
	}

	public void OnDeath()
	{
		Log.Info( $"WanderersNpc.OnDeath: {GameObject.Name} died at {WorldPosition}" );
		_isDead = true;
		CreateRagdoll();
		RespawnAfterDelay();
		GameObject.Destroy();
	}

	public void TakeDamage( int damage, Vector3 hitPosition, Vector3 force )
	{
		if ( _isDead )
		{
			Log.Info( $"WanderersNpc.TakeDamage: ignored damage because {GameObject.Name} is already dead" );
			return;
		}

		Log.Info( $"WanderersNpc.TakeDamage: {GameObject.Name} took {damage} at {hitPosition}, health before = {Health}" );
		Health -= damage;
		Log.Info( $"WanderersNpc.TakeDamage: {GameObject.Name} health after = {Health}" );

		if ( Health <= 0 )
		{
			Health = 0;
			OnDeath();
		}
	}

	private void CreateRagdoll()
	{
		if ( _bodyRenderer == null )
		{
			Log.Warning( $"WanderersNpc.CreateRagdoll: no body renderer on {GameObject.Name}" );
			return;
		}

		Log.Info( $"WanderersNpc.CreateRagdoll: creating ragdoll for {GameObject.Name}" );
		var ragdoll = new GameObject( true, $"{GameObject.Name} Ragdoll" );
		ragdoll.Tags.Add( "ragdoll", "solid", "debris" );
		ragdoll.WorldTransform = GameObject.WorldTransform;

		var ragdollRenderer = ragdoll.Components.Create<SkinnedModelRenderer>();
		ragdollRenderer.CopyFrom( _bodyRenderer );
		ragdollRenderer.UseAnimGraph = false;

		var physics = ragdoll.Components.Create<ModelPhysics>();
		physics.Model = ragdollRenderer.Model;
		physics.Renderer = ragdollRenderer;
		physics.CopyBonesFrom( _bodyRenderer, true );
		physics.MotionEnabled = true;

		ragdoll.DestroyAsync( 10f );
	}

	private async void RespawnAfterDelay()
	{
		await Task.DelaySeconds( 3f );
		Log.Info( $"WanderersNpc.RespawnAfterDelay: respawning {GameObject.Name} at {SpawnPosition}" );
		WandererPrefab.Clone( SpawnPosition );
	}
}
