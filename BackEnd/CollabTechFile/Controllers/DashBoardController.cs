using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]

public class DashBoardController : ControllerBase
{
    private readonly IDashBoardRepository _dashBoardRepository;

    public DashBoardController(IDashBoardRepository dashBoardRepository)
    {
        _dashBoardRepository = dashBoardRepository;
    }

    [HttpGet]
    public IActionResult Get()
    {
        // Explicitly specify the method to resolve ambiguity
        var dashboard = _dashBoardRepository.ObterDashboard();
        return Ok(dashboard);
    }
}
