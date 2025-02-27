# Debugging Pulsar Project Generation and Compilation

Based on our investigation, we've identified several issues with the project generation and compilation process:

## Key Issues

1. **System Config and Rule Sensor Mismatch**: 
   - The system_config.yaml defines valid sensors that must match what's used in the rules
   - There's an inconsistency between what's defined in system_config.yaml and what's used in sample-rules.yaml

2. **Rule Format Validation**:
   - The rule parser expects conditions and actions in specific formats
   - Sample rule was failing validation for missing required conditions

3. **Compiler Pipeline Issues**:
   - When generating the project, validations are performed but error messages aren't clear
   - Attempting to build the generated code fails because of missing project files

## Recommended Next Steps

1. **Create Valid Test Configuration**:
   - Ensure system config and rules use the same sensor names
   - Test with minimal, known-good configurations

2. **Debug Project Generation**:
   - Add logging to the TemplateManager to verify all template files are getting copied
   - Ensure the proper directory structure is created for code generation

3. **Fix RuntimeValidationFixture**:
   - Update to use a known-working config and rules
   - Add more detailed logging to project build process
   - Ensure proper namespace handling for the generated code

4. **Create Integration Tests**:
   - Test the full pipeline from rule definition to compiled code
   - Verify code generation, compilation, and execution independently

These steps will help ensure the Pulsar to Beacon compilation process is reliable and properly tested.
