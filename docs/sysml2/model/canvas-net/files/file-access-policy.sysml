package CanvasNet {
    part def FileAccessPolicy {
        doc /* The public FileAccessPolicy hook (an abstract class with a Name and Check) with its FileAccessRequest (reference, referrer, base directory, requested path, real path of the open handle) and FileAccessDecision (allowed, or denied with a reason and hint) types and the built-in policies: AllowAll ("any", the default, needs no real path), UnderRoots ("roots": the written path and the real path must each lie inside a root, with a lexical pre-check before the open), NoSymlinks ("no-symlinks": the real path must equal the written path below the deepest folder shared with the base directory, which is trusted), Combine (all parts must allow, the first denial wins, names joined with +) and FromDelegate (a named function). Policies are immutable and thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Files/FileAccessPolicy.cs, src/DemaConsulting.CanvasNet/Files/FileAccessPolicy.BuiltIns.cs, src/DemaConsulting.CanvasNet/Files/FileAccessRequest.cs, src/DemaConsulting.CanvasNet/Files/FileAccessDecision.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Files/FileAccessPolicyTests.cs */
        comment designRef /* Design: docs/design/canvas-net/files/file-access-policy.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/files/file-access-policy.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/files/file-access-policy.yaml */
    }
}
