using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaintMixer.Application;
using PaintMixer.ViewModels;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PaintMixer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("5PerSecond")]
    public class PaintMix : ControllerBase
    {
        PaintMixerDeviceEmulator PaintEmulator;
        public PaintMix(PaintMixerDeviceEmulator paintEmulator)
        {
            PaintEmulator = paintEmulator;
        }

        [HttpPost("FromValues")]
        public async Task<IActionResult> MixColorsFromValues(int Red, int Green, int Blue, int Black, int White, int Yellow)
        {
            //pick up values from query string and build the model for validation

            ColoringModel colorsModel = new ColoringModel();

            colorsModel.Red = Red;
            colorsModel.Black = Black;
            colorsModel.Yellow = Yellow;
            colorsModel.White = White;
            colorsModel.Blue = Blue;
            colorsModel.Green = Green;

            // validate the model

            var errorResults = new List<ValidationResult>();

            bool IsValid = Validator.TryValidateObject(colorsModel, new ValidationContext(colorsModel), errorResults, true);

            if (IsValid == false)
            {
                ApiErrorModel apiError = new ApiErrorModel() { ErrorMessages = errorResults.Select(s => s.ErrorMessage).ToList() };
                return BadRequest(apiError);
            }

            return await MixColorsFromModel(colorsModel);

        }

        [HttpPost("FromModel")]
        public async Task<IActionResult> MixColorsFromModel([FromBody] ColoringModel colorsModel)
        {
            if (ModelState.IsValid == false)
            {
                ApiErrorModel apiError = new ApiErrorModel() { ErrorMessages = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList() };
                return BadRequest(apiError);
            }

            //ask the "external service" to mix the colors

            try
            {
                int returnedCode = PaintEmulator.SubmitJob(red: colorsModel.Red, black: colorsModel.Black, white: colorsModel.White, yellow: colorsModel.Yellow, blue: colorsModel.Blue, green: colorsModel.Green);

                ApiResponseModel apiResponse = new ApiResponseModel() {

                    Code = returnedCode,
                    Type = (returnedCode == -1) ? ApiResponseTypes.Error.ToString() : ApiResponseTypes.Success.ToString(),
                    Description = (returnedCode == -1) ? "Job aborted due to invalid data or max capacity reached." : $"Successfuly created a new job with ID {returnedCode}"

                };

                if (returnedCode == -1) return Conflict(apiResponse);
                else return Ok(apiResponse);

            }
            catch (Exception ex)
            {
                ApiErrorModel apiError = new ApiErrorModel();
                apiError.ErrorMessages.Add(ex.Message);

                return StatusCode(500, apiError);
            }

        }

        [HttpGet("Job/{JobId:int}/status")]
        public async Task<IActionResult> GetJobStatus(int JobId)
        {
            try
            {
                int returnedCode = PaintEmulator.QueryJobState(JobId);

                ApiResponseModel apiResponse = new ApiResponseModel()
                {
                    Code = returnedCode,
                    Type = returnedCode switch
                    {
                        -1 => ApiResponseTypes.Warning.ToString(),
                        (0 | 1) => ApiResponseTypes.Warning.ToString(),
                        _ => ApiResponseTypes.Error.ToString()
                    },
                    Description = returnedCode switch { 
                        -1 => $"The job with ID {JobId} does not exist.",
                        0 => $"The job with ID {JobId} is still in the queue.",
                        1 => $"The job with ID {JobId} has been completed",
                        _ => $"Unknown status for job ID {JobId}."
                    } 

                };

                if (returnedCode == -1) return NotFound(apiResponse);                

                return Ok(apiResponse);

            }
            catch (Exception ex)
            {
                ApiErrorModel apiError = new ApiErrorModel();
                apiError.ErrorMessages.Add(ex.Message);

                return StatusCode(500, apiError);
            }
        }

        [HttpDelete("Job/{JobId:int}/cancel")]
        public async Task<IActionResult> CancelJob([FromRoute] int JobId)
        {
            try
            {
                int returnedCode = PaintEmulator.CancelJob(JobId);

                ApiResponseModel apiResponse = new ApiResponseModel()
                {
                    Code = returnedCode,
                    Type = returnedCode switch
                    {
                        -1 => ApiResponseTypes.Error.ToString(),
                        0 => ApiResponseTypes.Success.ToString(),
                        _ => ApiResponseTypes.Warning.ToString()
                    },
                    Description = returnedCode switch
                    {
                        -1 => $"Job with ID {JobId} does not exist or could not be canceled.",
                        0 => $"The job with ID {JobId} was successfuly canceled.",
                        _ => $"Unknown cancelation status for job ID {JobId}."
                    }

                };

                if (returnedCode == -1) return UnprocessableEntity(apiResponse);

                return Ok(apiResponse);

            }
            catch (Exception ex)
            {
                ApiErrorModel apiError = new ApiErrorModel();
                apiError.ErrorMessages.Add(ex.Message);

                return StatusCode(500, apiError);
            }
        }

    }
}
