## DemaConsulting.TestResults Verification

This document provides the verification evidence for the `DemaConsulting.TestResults` OTS software item.
Requirements for this OTS item are defined in the DemaConsulting.TestResults OTS Software Requirements document.

### Required Functionality

`DemaConsulting.TestResults` (including its `TestResults.IO` serializers) is a runtime NuGet dependency of the
CanvasNet tool. The `Validation` unit uses it to record the outcome of each self-test and to serialize the
collection to a TRX file or a JUnit XML file. Correct serialization is required because ReqStream and the CI
systems consume those files as the qualification and platform evidence.

### Verification Approach

`DemaConsulting.TestResults` has no CLI or self-validation mode, so it is verified indirectly, end to end,
through its observable effect: the self-validation is run with a results file, and the produced document is
checked. A passing test run for each scenario below is direct evidence that the library records and serializes
the self-test results as expected. The platform requirements additionally rely on the TRX files the installed
tool writes in the integration-test job, which ReqStream parses; a file that could not be parsed would fail the
requirements enforcement.

### Test Scenarios

#### Validation_Run_WithTrxResultsFile_WritesTrxFile

**Scenario**: `Validation.Run` records two self-tests and serializes them with `TrxSerializer` to a `.trx` path.

**Expected**: the file exists, is a TRX document (`TestRun` element), and names both self-tests.

**Requirement coverage**: `CanvasNet-OTS-TestResults`.

#### Validation_Run_WithXmlResultsFile_WritesXmlFile

**Scenario**: `Validation.Run` serializes the same results with `JUnitSerializer` to an `.xml` path.

**Expected**: the file exists and is a JUnit document (`testsuites` element).

**Requirement coverage**: `CanvasNet-OTS-TestResults`.

#### CanvasNetTool_ValidateWithTrxResults_Requested_GeneratesTrxFile

**Scenario**: the built tool is run as a process with `--validate --results <path>.trx`.

**Expected**: the process succeeds and the TRX file written by the library has the expected structure.

**Requirement coverage**: `CanvasNet-OTS-TestResults`.

### Requirements Coverage

- **`CanvasNet-OTS-TestResults`**: Validation_Run_WithTrxResultsFile_WritesTrxFile,
  Validation_Run_WithXmlResultsFile_WritesXmlFile,
  CanvasNetTool_ValidateWithTrxResults_Requested_GeneratesTrxFile
