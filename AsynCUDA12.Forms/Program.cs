using Microsoft.Extensions.Configuration;

namespace AsynCUDA12.Forms
{
	internal static class Program
	{
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
				.Build();

			int id = int.TryParse(config["PreferredDeviceId"], out int parsedId) ? parsedId : 0;

			ApplicationConfiguration.Initialize();
			Application.Run(new WindowMain(id));
		}
	}
}