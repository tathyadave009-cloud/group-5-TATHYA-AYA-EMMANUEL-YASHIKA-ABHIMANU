using Microsoft.AspNetCore.Mvc;
namespace Finance.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase  
    {
        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "Healthy", service = "BudgetFinance", version = "0.1.0" });
        }
    }
}
