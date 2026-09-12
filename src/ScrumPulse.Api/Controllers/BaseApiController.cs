namespace ScrumPulse.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("global")]
public abstract class BaseApiController : ControllerBase { }
