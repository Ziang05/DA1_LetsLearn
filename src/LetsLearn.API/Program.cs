using LetsLearn.API.BackgroundServices;
using LetsLearn.API.Middleware;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using LetsLearn.Infrastructure.Redis;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.Infrastructure.UnitOfWork;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services;
using LetsLearn.UseCases.Services.AssignmentResponseService;
using LetsLearn.UseCases.Services.Auth;
using LetsLearn.UseCases.Services.CommentService;
using LetsLearn.UseCases.Services.ConversationService;
using LetsLearn.UseCases.Services.CourseClone;
using LetsLearn.UseCases.Services.CourseSer;
using LetsLearn.UseCases.Services.MessageService;
using LetsLearn.UseCases.Services.QuestionSer;
using LetsLearn.UseCases.Services.QuizResponseService;
using LetsLearn.UseCases.Services.SectionSer;
using LetsLearn.UseCases.Services.UserSer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using LetsLearn.API.Hubs;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
// SignalR — cho phép real-time messaging
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "LetsLearn.API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter Access Token here"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<LetsLearnContext>(options =>
        options.UseNpgsql(connectionString));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "LetsLearn";
});
builder.Services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();

// DI for custom repositories
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IAssignmentResponseRepository, AssignmentResponseRepository>();
builder.Services.AddScoped<IQuizResponseRepository, QuizResponseRepository>();
builder.Services.AddScoped<IQuizResponseAnswerRepository, QuizResponseAnswerRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

//DI for custom services
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IAssignmentResponseService, AssignmentResponseService>();
builder.Services.AddScoped<IQuizResponseService, QuizResponseService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<ITopicService, TopicService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICourseCloneService, CourseCloneService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHostedService<DeadlineReminderBackgroundService>();

builder.Services.AddSingleton<CourseFactory>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "ManualJwt";
    options.DefaultChallengeScheme = "ManualJwt";
    options.DefaultForbidScheme = "ManualJwt";
})
.AddScheme<AuthenticationSchemeOptions, JwtAuthHandler>("ManualJwt", null);

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder
            .WithOrigins("http://localhost:4200", "http://localhost:3000", "http://localhost:3001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(origin => true)
            .AllowCredentials()); // AllowCredentials bắt buộc cho SignalR WebSocket
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//Only uncomment this if you want to auto apply migrations in dev environment
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<LetsLearnContext>();
    dbContext.Database.EnsureCreated();
    
    // Ensure Payments table exists (EnsureCreated won't add it if DB already exists)
    var createPaymentsSql = @"
        CREATE TABLE IF NOT EXISTS ""Payments"" (
            ""Id"" uuid NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""CourseId"" text NOT NULL,
            ""Amount"" numeric NOT NULL,
            ""Description"" text,
            ""OrderId"" text,
            ""TransactionId"" text,
            ""Status"" text NOT NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL,
            ""PaidAt"" timestamp with time zone,
            CONSTRAINT ""PK_Payments"" PRIMARY KEY (""Id"")
        );";
    await dbContext.Database.ExecuteSqlRawAsync(createPaymentsSql);

    try 
    {
        Console.WriteLine("---- STARTING TABLE CREATION & ALTERATION ----");
        var createTopicMeetingsSql = @"
            CREATE TABLE IF NOT EXISTS ""TopicMeetings"" (
                ""TopicId"" uuid NOT NULL,
                ""Description"" text,
                ""Open"" timestamp with time zone,
                ""Close"" timestamp with time zone,
                ""MeetingLink"" text,
                CONSTRAINT ""PK_TopicMeetings"" PRIMARY KEY (""TopicId""),
                CONSTRAINT ""FK_TopicMeetings_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
            );";
        await dbContext.Database.ExecuteSqlRawAsync(createTopicMeetingsSql);
        
        // Add MeetingLink column if table already existed but without this column
        var alterTopicMeetingsSql = @"ALTER TABLE ""TopicMeetings"" ADD COLUMN IF NOT EXISTS ""MeetingLink"" text;";
        await dbContext.Database.ExecuteSqlRawAsync(alterTopicMeetingsSql);

        Console.WriteLine("---- TOPIC MEETINGS TABLE CHECKED ----");

        var createTopicMeetingHistoriesSql = @"
            CREATE TABLE IF NOT EXISTS ""TopicMeetingHistories"" (
                ""Id"" uuid NOT NULL,
                ""TopicMeetingId"" uuid NOT NULL,
                ""StartTime"" timestamp with time zone NOT NULL,
                ""EndTime"" timestamp with time zone,
                ""AttendeeCount"" integer NOT NULL,
                ""AttendanceCsvUrl"" text,
                CONSTRAINT ""PK_TopicMeetingHistories"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_TopicMeetingHistories_TopicMeetings_TopicMeetingId"" FOREIGN KEY (""TopicMeetingId"") REFERENCES ""TopicMeetings"" (""TopicId"") ON DELETE CASCADE
            );";
        await dbContext.Database.ExecuteSqlRawAsync(createTopicMeetingHistoriesSql);
        Console.WriteLine("---- TOPIC MEETING HISTORIES TABLE CHECKED ----");
    }
    catch (Exception ex)
    {
        Console.WriteLine("---- ERROR CREATING/ALTERING TABLES: " + ex.ToString());
    }

    // Seed admin user
    var authService = services.GetRequiredService<IAuthService>();
    var userRepo = services.GetRequiredService<IUserRepository>();

    var adminEmail = "admin@letslearn.com";
    var existingAdmin = await userRepo.GetByEmailAsync(adminEmail);

    if (existingAdmin == null)
    {
        var adminRequest = new SignUpRequest
        {
            Email = adminEmail,
            Password = "admin",
            Username = "admin",
            Role = LetsLearn.Core.Shared.AppRoles.Admin
        };

        try
        {
            // Create a minimal HttpContext for the registration
            var httpContext = new DefaultHttpContext();
            await authService.RegisterAsync(adminRequest, httpContext);
            Console.WriteLine("Admin user created successfully!");
            Console.WriteLine($"Email: {adminEmail}");
            Console.WriteLine("Password: admin");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create admin user: {ex.Message}");
        }
    }
}

app.UseCors("AllowFrontend");

app.UseStaticFiles(); // Enable local file serving for uploads

app.UseAuthentication();

app.UseJwtAuth();

app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub — FE kết nối tới "/hubs/chat"
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
