using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WebApp_MVC_.Controllers
{
    // MVC Controller for handling both views and API requests
    public class ValidationController : Controller
    {
        // Action to render the Index view (MVC page)
        public IActionResult Index()
        {
            return View();
        }

        // API action to handle POST requests for validating JSX code
        [HttpPost("api/validation/validate")]
        public IActionResult ValidateForm([FromBody] Models.JsonRequest request)
        {
            try
            {
                // Check if the JsxCode field is provided in the request
                if (string.IsNullOrWhiteSpace(request?.JsxCode))
                {
                    return BadRequest("The jsxCode field is required.");
                }

                // Extract validation rules from the provided JSX code
                var validationRules = ExtractValidationRules(request.JsxCode);

                // Convert extracted validation rules to JSON format and return them
                string jsonResult = JsonConvert.SerializeObject(validationRules, Formatting.Indented);
                return Ok(jsonResult);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing the form: {ex.Message}");
            }
        }

        // Method to extract validation rules from JSX code
        public static Dictionary<string, object> ExtractValidationRules(string jsxCode)
        {
            var rules = new Dictionary<string, object>();

            // 1. Required Field Validation (e.g., 'Email is required')
            var requiredPattern = new Regex(@"if\s*\(\s*!formData\.(\w+)\s*\)", RegexOptions.IgnoreCase);
            var requiredMatches = requiredPattern.Matches(jsxCode);

            foreach (Match match in requiredMatches)
            {
                var fieldName = match.Groups[1].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["required"] = $"{fieldName} is required";
            }

            // 2. Regex Validation (e.g., Email regex)
            var regexPattern = new Regex(@"/(.+?)\.test\(formData\.(\w+)\)", RegexOptions.IgnoreCase);
            var regexMatches = regexPattern.Matches(jsxCode);

            foreach (Match match in regexMatches)
            {
                var fieldName = match.Groups[2].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["regex"] = new
                {
                    pattern = match.Groups[1].Value,
                    message = $"{fieldName} is invalid"
                };
            }

            // 3. Range Validation (e.g., Age must be between 18 and 100)
            var rangePattern = new Regex(@"formData\.(\w+)\s*<\s*(\d+)\s*\|\|\s*formData\.(\w+)\s*>=\s*(\d+)", RegexOptions.IgnoreCase);
            var rangeMatches = rangePattern.Matches(jsxCode);

            foreach (Match match in rangeMatches)
            {
                var fieldName = match.Groups[1].Value;
                var min = int.Parse(match.Groups[2].Value);
                var max = int.Parse(match.Groups[4].Value);

                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["range"] = new
                {
                    min,
                    max,
                    message = $"{fieldName} must be between {min} and {max}"
                };
            }

            // 4. Min Length Validation for Strings
            var minLengthPattern = new Regex(@"formData\.(\w+)\.length\s*<\s*(\d+)", RegexOptions.IgnoreCase);
            var minLengthMatches = minLengthPattern.Matches(jsxCode);

            foreach (Match match in minLengthMatches)
            {
                var fieldName = match.Groups[1].Value;
                var minLength = int.Parse(match.Groups[2].Value);

                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["minLength"] = new
                {
                    minLength,
                    message = $"{fieldName} must be at least {minLength} characters long"
                };
            }

            return rules;
        }
    }
}
