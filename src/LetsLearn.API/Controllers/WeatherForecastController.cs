using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRedisCacheService _cache;

        public WeatherForecastController(ILogger<WeatherForecastController> logger,
            IUnitOfWork unitOfWork,
            IRedisCacheService cache)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<IActionResult> Get(CancellationToken ct = default)
        {
            var cachingKey = "forecast";
            var weather = await _cache.GetAsync<IEnumerable<WeatherForecast?>>(cachingKey, 150, ct);
            if (weather is not null)
            {
                _logger.LogInformation("Retrieved all courses from cache");
                return Ok(weather);
            }
            weather = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            });
            _cache.Set(cachingKey, weather);
            return Ok(weather);
        }
        [HttpGet("db")]
        public async Task<IActionResult> GetWeatherForecastDB()
        {
            var weatherForecasts = await _unitOfWork.WeatherForecasts.GetAllAsync();
            if (weatherForecasts == null || !weatherForecasts.Any())
            {
                return NotFound("No weather forecasts found.");
            }
            return Ok(weatherForecasts);
        }

        [HttpPost]
        public async Task<IActionResult> AddWeatherForecast([FromBody] WeatherForecast weatherForecast)
        {
            if (weatherForecast == null)
            {
                return BadRequest("Weather forecast cannot be null.");
            }
            await _unitOfWork.WeatherForecasts.AddAsync(weatherForecast);
            await _unitOfWork.CommitAsync();
            return Ok();
        }
    }
}
