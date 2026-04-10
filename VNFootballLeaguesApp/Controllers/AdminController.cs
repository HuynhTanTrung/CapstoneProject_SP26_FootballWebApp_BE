using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet("getAllUser")]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _adminService.GetAllUsersAsync();
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet("getUser")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var result = await _adminService.GetUserByIdAsync(userId);
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return NotFound(result);
        }

        [HttpPut("updateUser")]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = false, message = "Invalid request data" });
            }

            var updatedUser = new User
            {
                Username = request.Username,
                Email = request.Email,
                FullName = request.FullName
            };

            var result = await _adminService.UpdateUserAsync(userId, updatedUser);
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpDelete("softDeleteUser")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            var result = await _adminService.DeleteUserAsync(userId);
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpDelete("deleteUser")]
        public async Task<IActionResult> PermanentDeleteUser(Guid userId)
        {
            var result = await _adminService.PermanentDeleteUserAsync(userId);
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet("userDashboard")]
        public async Task<IActionResult> GetUserDashboardStatistics()
        {
            var result = await _adminService.GetUserDashboardStatisticsAsync();
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet("incomeDashboard")]
        public async Task<IActionResult> GetMoneyEarnedDashboard()
        {
            var result = await _adminService.GetMoneyEarnedDashboardAsync();
            var status = result.GetType().GetProperty("status")?.GetValue(result) as bool?;

            if (status == true)
                return Ok(result);

            return BadRequest(result);
        }
    }
}
