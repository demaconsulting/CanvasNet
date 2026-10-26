### ImageWriter

<!-- cspell:ignore canvasnet jpeg tiff tif jpg overwritable -->

#### Verification Approach

`ImageWriter` is verified with unit tests in `ImageWriterTests.cs`. Surfaces are generated in memory (an opaque
gradient and one with transparency), written into a temporary directory, and decoded again with the codec of the
output format, so the expected pixels come from the generator and the decode from the codec, never from the writer.
The overwrite and atomic-write behavior is checked by looking at the directory contents after success and after
failure, and a failing write is provoked by putting a file where the output's directory would have to be.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The format follows the option, then the extension; mistakes are usage errors.
- PNG, BMP, and TIFF outputs decode to exactly the pixels written, including alpha; JPEG outputs are close.
- Transparency is flattened over the background for JPEG only.
- An existing output is overwritten by default and kept with no-clobber; an output that is the input is refused.
- No temporary file is ever left behind; the same pixels give the same bytes.

#### Test Scenarios

**ImageWriter_ResolveFormat_Extension_SelectsFormat**, **ImageWriter_ResolveFormat_FormatOption_WinsOverExtension**,
**ImageWriter_ResolveFormat_UnknownFormat_ThrowsUsageError**, **ImageWriter_ExtensionFormats_AreWellFormed**, and
**ImageWriter_Write_FormatOption_EncodesThatFormat**: every extension (in any case, also in a directory name with
dots) selects its format; the option, with its short names and surrounding spaces, wins over an unrelated extension;
an unknown name, no extension, and an unknown extension are usage errors that say what to do; the table is lower
case with a dot and covers every format; an output named `.dat` is encoded as the format named.

**ImageWriter_Write_LosslessFormat_KeepsEveryPixelIncludingAlpha**: PNG, BMP, and TIFF outputs of an image with
transparency decode to exactly the same pixels, and the result reports the path, size, and file length.

**ImageWriter_Write_JpegOfOpaqueImage_IsCloseAndNotFlattened**,
**ImageWriter_Write_JpegOfTransparentImage_FlattensOverBackground**, **ImageWriter_Write_JpegQuality_ChangesFileSize**,
**ImageWriter_WriteOptionsDefault_HasDocumentedDefaults**, and **ImageWriter_Write_InvalidArguments_Throw**: a JPEG
of an opaque image is close and not flattened; a fully transparent image becomes white or black as the flatten
background says, is reported as flattened, and the source surface is unchanged; a high quality gives a larger file
than a low one; the defaults are the documented ones; null arguments and a quality outside 1 to 100 throw.

**ImageWriter_Write_ExistingOutput_IsOverwrittenWithoutLeftovers**, **ImageWriter_Write_MissingDirectory_IsCreated**,
and **ImageWriter_Write_DirectoryCannotBeCreated_ThrowsOutputWriteFailed**: an existing file is replaced by the new
image and the directory then holds only the output; a missing directory path is created; a blocked directory (a file
where a folder should be) is `output-write-failed` naming the output and the file in the way.

**ImageWriter_Write_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile** and
**ImageWriter_Write_NoClobberAndNewOutput_WritesFile**: no-clobber refuses an existing file, leaves it untouched and
leaves no temporary file, and still writes a new one.

**ImageWriter_Write_OutputIsInput_ThrowsSameInputOutput** and **ImageWriter_CheckTarget_Problems_AreReported**: an
output named through a different spelling of the input path is refused and the input is untouched;
`CheckTarget` reports the input as output, an existing directory, and (with no-clobber) an existing file, and accepts a
fresh path and an overwritable file. _Not verified, by design:_ a hard link to the input is not detected (the guard
compares paths after normalization and link resolution, not file identity); it is safe because the input is read
completely before any output is written and the output is a new temporary file moved over the target name.

**ImageWriter_CheckTarget_LinkedDirectory_IsSameFile** and **ImageWriter_CheckTarget_LinkTargetThroughLink_IsSameFile**:
an output reaching the input through a symbolic link to its folder, or through a link whose stored target itself goes
through another link (as `/var` does on macOS), is refused as `same-input-output`, while another name in the same
folder is accepted; the tests skip themselves only where the operating system refuses to create symbolic links.

**ImageWriter_CheckTarget_UnusablePath_IsOutputWriteFailed**: an empty path, a path with a null character, and a path
below a file are `output-write-failed` (the last names the file that is in the way); a folder that does not exist is
accepted.

**ImageWriter_Write_LongFileName_IsWritten**: a file name of 244 characters is written and the folder holds only the
output, because the temporary file does not take its name from the target.

**ImageWriter_Write_SamePixelsTwice_WritesIdenticalBytes**: the same image written twice as each format gives the
same bytes.
