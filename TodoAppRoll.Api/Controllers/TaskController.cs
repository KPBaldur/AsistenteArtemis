using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;
using TodoAppRoll.Api.Models.Enums;
using Task = TodoAppRoll.Api.Models.Entities.Task;
using TaskStatus = TodoAppRoll.Api.Models.Enums.TaskStatus;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TaskController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        // GET: api/Task
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks()
        {
            var userId = GetUserId();
            var tasks = await _context.Tasks
                .Include(t => t.Subtasks)
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.Order)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    Priority = t.Priority,
                    Order = t.Order,
                    CompletedAt = t.CompletedAt,
                    CreatedAt = t.CreatedAt,
                    AlarmId = t.AlarmId,
                    Subtasks = t.Subtasks.Select(s => new SubtaskDto
                    {
                        Id = s.Id,
                        Title = s.Title,
                        IsDone = s.IsDone,
                        Order = s.Order
                    }).OrderBy(s => s.Order).ToList()
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // POST: api/Task
        [HttpPost]
        public async Task<ActionResult<TaskDto>> CreateTask(TaskCreateDto createDto)
        {
            var userId = GetUserId();
            var task = new Task
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = createDto.Title,
                Description = createDto.Description,
                Priority = createDto.Priority,
                Order = createDto.Order,
                AlarmId = createDto.AlarmId,
                CreatedAt = DateTime.UtcNow,
                Status = TaskStatus.Pending
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Order = task.Order,
                CreatedAt = task.CreatedAt,
                AlarmId = task.AlarmId
            });
        }

        // PUT: api/Task/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(Guid id, TaskUpdateDto updateDto)
        {
            var userId = GetUserId();
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null) return NotFound();

            if (updateDto.Title != null) task.Title = updateDto.Title;
            if (updateDto.Description != null) task.Description = updateDto.Description;
            if (updateDto.Priority.HasValue) task.Priority = updateDto.Priority.Value;
            if (updateDto.Order.HasValue) task.Order = updateDto.Order.Value;
            if (updateDto.AlarmId.HasValue) task.AlarmId = updateDto.AlarmId;

            if (updateDto.Status.HasValue)
            {
                if (updateDto.Status.Value == TaskStatus.Completed && task.Status != TaskStatus.Completed)
                {
                    task.CompletedAt = DateTime.UtcNow;
                }
                else if (updateDto.Status.Value != TaskStatus.Completed)
                {
                    task.CompletedAt = null;
                }
                task.Status = updateDto.Status.Value;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Task/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            var userId = GetUserId();
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null) return NotFound();

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- Subtasks ---

        // POST: api/Task/{taskId}/subtask
        [HttpPost("{taskId}/subtask")]
        public async Task<ActionResult<SubtaskDto>> CreateSubtask(Guid taskId, SubtaskCreateDto createDto)
        {
            var userId = GetUserId();
            var task = await _context.Tasks.AnyAsync(t => t.Id == taskId && t.UserId == userId);
            if (!task) return NotFound(new { message = "Tarea no encontrada" });

            var subtask = new Subtask
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                Title = createDto.Title,
                Order = createDto.Order,
                IsDone = false
            };

            _context.Subtasks.Add(subtask);
            await _context.SaveChangesAsync();

            return Ok(new SubtaskDto
            {
                Id = subtask.Id,
                Title = subtask.Title,
                IsDone = subtask.IsDone,
                Order = subtask.Order
            });
        }

        // PATCH: api/Task/subtask/{subtaskId}/toggle
        [HttpPatch("subtask/{subtaskId}/toggle")]
        public async Task<IActionResult> ToggleSubtask(Guid subtaskId)
        {
            var userId = GetUserId();
            var subtask = await _context.Subtasks
                .Include(s => s.Task)
                .FirstOrDefaultAsync(s => s.Id == subtaskId && s.Task.UserId == userId);

            if (subtask == null) return NotFound();

            subtask.IsDone = !subtask.IsDone;
            await _context.SaveChangesAsync();
            return Ok(new { isDone = subtask.IsDone });
        }
    }
}
