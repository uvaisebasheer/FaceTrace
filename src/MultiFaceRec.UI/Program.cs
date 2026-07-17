using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiFaceRec.App.Services;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Data;
using MultiFaceRec.UI.Forms;
using MultiFaceRec.Vision;

namespace MultiFaceRec.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        
        var recognitionService = provider.GetRequiredService<RecognitionService>();
        recognitionService.WarmUpAsync().GetAwaiter().GetResult();

        var loginForm = provider.GetRequiredService<LoginForm>();
        Application.Run(loginForm);
    }

    private static void ConfigureServices(ServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        string dbPath = Path.Combine(AppContext.BaseDirectory, configuration["Database:Path"] ?? "multifacerec.db");
        services.AddSingleton(new SqliteConnectionFactory(dbPath));

        services.AddSingleton<IPersonRepository, SqlitePersonRepository>();
        services.AddSingleton<IFaceEmbeddingRepository, SqliteFaceEmbeddingRepository>();
        services.AddSingleton<IUserRepository, SqliteUserRepository>();

        string yunetPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuration["Models:YuNetPath"]!));
        string sfacePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuration["Models:SFacePath"]!));
        float scoreThreshold = float.Parse(configuration["Detection:ScoreThreshold"] ?? "0.8");
        float nmsThreshold = float.Parse(configuration["Detection:NmsThreshold"] ?? "0.3");
        float similarityThreshold = float.Parse(configuration["Recognition:SimilarityThreshold"] ?? "0.363");

        services.AddSingleton<IFaceDetector>(_ => new YuNetFaceDetector(yunetPath, scoreThreshold, nmsThreshold));
        string? debugCropFolder = configuration["Recognition:DebugSaveAlignedCropsTo"];
        if (string.IsNullOrWhiteSpace(debugCropFolder)) debugCropFolder = null;
        else debugCropFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, debugCropFolder));

        string? debugLogFile = debugCropFolder is not null
            ? Path.Combine(debugCropFolder, "embedding_debug_log.txt")
            : null;

        services.AddSingleton<IFaceEmbedder>(_ => new SFaceEmbedder(sfacePath, debugCropFolder, debugLogFile));
        services.AddSingleton<IFaceMatcher>(sp => new CosineFaceMatcher(
            sp.GetRequiredService<IFaceEmbeddingRepository>(), similarityThreshold));

        services.AddSingleton<AuthService>();
        services.AddSingleton<EnrollmentService>();
        services.AddSingleton(sp => new RecognitionService(
            sp.GetRequiredService<IFaceDetector>(),
            sp.GetRequiredService<IFaceEmbedder>(),
            sp.GetRequiredService<IFaceMatcher>(),
            debugCropFolder is not null ? Path.Combine(debugCropFolder, "pairwise_similarity_log.txt") : null));
        services.AddTransient<VideoIngestService>();

        services.AddTransient<LoginForm>();
        services.AddTransient<RegisterForm>();
        services.AddTransient<MainForm>();
    }
}
