using System.Threading.Tasks;

public static partial class SceneParticlesExtensions
{
	public static async void PlayUntilFinished( this SceneParticles particles, TaskSource source )
	{
		try
		{
			while ( !particles.Finished )
			{
				await source.Frame();
				particles.Simulate( Time.Delta );
			}
		}
		catch ( TaskCanceledException )
		{
		}

		particles.Delete();
	}
}
