# Live tutoring changed-file inventory

Relative to baseline `5b7b192`, including the preserved PR #2 merge. 73 files; no deleted files. Imported tool implementation and test files retain their original commit history. File paths are repository-relative.

| Change | File | Purpose |
| --- | --- | --- |
| Added | `Pointly.App/MainWindow.Goals.cs` | Shared goal planning, clarification, fresh observation, verification, replanning and cancellation. |
| Added | `Pointly.App/MainWindow.Tools.cs` | File/workspace selection, review UI, execution lifecycle and retained results. |
| Modified | `Pointly.App/MainWindow.xaml.cs` | Connect typed/voice entry, busy state, verification, target context and contextual cues. |
| Modified | `Pointly.App/Pointly.App.csproj` | Reference the imported tools library from the active desktop app. |
| Modified | `Pointly.App/Presentation/BuddySurface.cs` | Window-bounded spotlight, contextual cue drawing and cleanup. |
| Added | `Pointly.App/Presentation/BusyBorder.cs` | 1.6-second border shine and reduced-motion static fallback. |
| Modified | `Pointly.App/Presentation/ChatWindow.xaml` | Busy controls, Cancel/Check again, file selection and selected-item display. |
| Modified | `Pointly.App/Presentation/ChatWindow.xaml.cs` | Input guards, focus preservation and composer events. |
| Modified | `Pointly.App/Presentation/GuidancePresenter.cs` | Coordinate composer state, capture hiding, cues and file-selection events. |
| Modified | `Pointly.App/Presentation/PresentationGeometry.cs` | Control-type cue selection and target-window bounds. |
| Added | `Pointly.App/Presentation/ToolReviewWindow.cs` | Readable preview/result window with explicit Run and Cancel. |
| Added | `Pointly.App/Tools/LocalToolDispatcher.cs` | Implemented tool registry, validated arguments, selected-path scopes and host-created approvals. |
| Added | `Pointly.App/Tools/ToolReview.cs` | Expiring single-use reviews, readable results and dependency configuration. |
| Added | `Pointly.App/Tutor/GoalSession.cs` | Typed planner outcomes and host-owned goal state/identity. |
| Added | `Pointly.App/Tutor/OpenRouterPlanner.cs` | Bounded cloud planning, selection fallback and evidence-based verification. |
| Modified | `Pointly.App/Voice/VoiceSession.Persistent.cs` | Preserve listening across unmute pulses; defer narration until the utterance commits. |
| Modified | `Pointly.App/Walkthrough/WalkthroughService.cs` | Add semantic expected-result data to existing walkthrough steps. |
| Modified | `Pointly.App/Windows/TargetContextWatcher.cs` | Preserve composer interactions; invalidate changed external contexts with diagnostics. |
| Added | `Pointly.Tests/ChatBusyTests.cs` | Disabled input, duplicate submission guard and input restoration. |
| Added | `Pointly.Tests/GoalSessionTests.cs` | Clarification/history, stale identity, action contracts and verification evidence. |
| Modified | `Pointly.Tests/PresentationTests.cs` | Control-type cue selection plus existing DPI/placement checks. |
| Added | `Pointly.Tests/ToolDispatcherTests.cs` | Tool registration, approval injection/replay, selected inputs, cancellation, changed sources and conflicts. |
| Modified | `README.md` | Current active app, launch, connected tools and cloud/local boundaries. |
| Modified | `design-qa.md` | Native render evidence and functional visual checks. |
| Modified | `docs/HACKATHON_COMPLIANCE.md` | Preserve baseline disclosure and identify new contributions/dependencies. |
| Modified | `docs/IMPLEMENTATION.md` | Preserve PR #2 tool implementation notes and link current Windows integration. |
| Added | `docs/LIVE_TUTORING.md` | Setup, execution flow, dependency versions, validation and limitations. |
| Added | `docs/LIVE_TUTORING_FILES.md` | This complete changed-file inventory. |
| Modified | `docs/README.md` | Updated documentation index and active library connection. |
| Added | `scripts/Setup-ToolDependencies.ps1` | Install/check public Windows dependencies and configure absolute paths. |
| Modified | `scripts/Start-Pindo.ps1` | Refresh public native-tool paths with existing locally saved cloud credentials. |
| Added | `src/LocalTutor.Tools/Compression/CompressImageTool.cs` | PR #2 implementation: Compression; CompressImageTool. |
| Added | `src/LocalTutor.Tools/Compression/ImageCompressionContracts.cs` | PR #2 implementation: Compression; ImageCompressionContracts. |
| Added | `src/LocalTutor.Tools/Compression/compress_image.schemas.json` | PR #2 implementation: Compression; compress_image.schemas. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/ConvertImageTool.cs` | PR #2 implementation: FileInspectionAndConversion; ConvertImageTool. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/FileAccessScope.cs` | PR #2 implementation: FileInspectionAndConversion; FileAccessScope. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/FileToolContracts.cs` | PR #2 implementation: FileInspectionAndConversion; FileToolContracts. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/ImageContent.cs` | PR #2 implementation: FileInspectionAndConversion; ImageContent. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/ImageMagickCodec.cs` | PR #2 implementation: FileInspectionAndConversion; ImageMagickCodec. Normalize ImageMagick 7 alpha-channel metadata on Windows. |
| Added | `src/LocalTutor.Tools/FileInspectionAndConversion/InspectFileTool.cs` | PR #2 implementation: FileInspectionAndConversion; InspectFileTool. |
| Added | `src/LocalTutor.Tools/FileOrganization/FindDuplicatesTool.cs` | PR #2 implementation: FileOrganization; FindDuplicatesTool. |
| Added | `src/LocalTutor.Tools/FileOrganization/OrganizationAccessScope.cs` | PR #2 implementation: FileOrganization; OrganizationAccessScope. |
| Added | `src/LocalTutor.Tools/FileOrganization/OrganizationContracts.cs` | PR #2 implementation: FileOrganization; OrganizationContracts. |
| Added | `src/LocalTutor.Tools/FileOrganization/OrganizationIO.cs` | PR #2 implementation: FileOrganization; OrganizationIO. |
| Added | `src/LocalTutor.Tools/FileOrganization/OrganizeApplyTool.cs` | PR #2 implementation: FileOrganization; OrganizeApplyTool. |
| Added | `src/LocalTutor.Tools/FileOrganization/OrganizePreviewTool.cs` | PR #2 implementation: FileOrganization; OrganizePreviewTool. |
| Added | `src/LocalTutor.Tools/Pdf/PdfContracts.cs` | PR #2 implementation: Pdf; PdfContracts. |
| Added | `src/LocalTutor.Tools/Pdf/PdfNative.cs` | PR #2 implementation: Pdf; PdfNative. |
| Added | `src/LocalTutor.Tools/Pdf/PdfTools.cs` | PR #2 implementation: Pdf; PdfTools. |
| Added | `src/LocalTutor.Tools/Pdf/pdf.schemas.json` | PR #2 implementation: Pdf; pdf.schemas. |
| Added | `src/LocalTutor.Tools/VideoDownload/DownloadVideoTool.cs` | PR #2 implementation: VideoDownload; DownloadVideoTool. |
| Added | `src/LocalTutor.Tools/VideoDownload/InspectVideoTool.cs` | PR #2 implementation: VideoDownload; InspectVideoTool. |
| Added | `src/LocalTutor.Tools/VideoDownload/NativeYtDlpProcess.cs` | PR #2 implementation: VideoDownload; NativeYtDlpProcess. |
| Added | `src/LocalTutor.Tools/VideoDownload/VideoContracts.cs` | PR #2 implementation: VideoDownload; VideoContracts. |
| Added | `src/LocalTutor.Tools/VideoDownload/VideoMetadata.cs` | PR #2 implementation: VideoDownload; VideoMetadata. |
| Added | `src/LocalTutor.Tools/VideoDownload/VideoUrl.cs` | PR #2 implementation: VideoDownload; VideoUrl. |
| Added | `src/LocalTutor.Tools/VideoDownload/YtDlpClient.cs` | PR #2 implementation: VideoDownload; YtDlpClient. Accept the pinned Windows FFmpeg packaging suffix. |
| Added | `src/LocalTutor.Tools/ZipArchive/ArchiveAccessScope.cs` | PR #2 implementation: ZipArchive; ArchiveAccessScope. |
| Added | `src/LocalTutor.Tools/ZipArchive/ArchiveContracts.cs` | PR #2 implementation: ZipArchive; ArchiveContracts. |
| Added | `src/LocalTutor.Tools/ZipArchive/ArchiveIO.cs` | PR #2 implementation: ZipArchive; ArchiveIO. |
| Added | `src/LocalTutor.Tools/ZipArchive/CreateZipTool.cs` | PR #2 implementation: ZipArchive; CreateZipTool. |
| Added | `src/LocalTutor.Tools/ZipArchive/ExtractZipTool.cs` | PR #2 implementation: ZipArchive; ExtractZipTool. |
| Added | `tests/LocalTutor.Tests/Compression/ImageCompressionTests.cs` | PR #2 validation: Compression; ImageCompressionTests. |
| Added | `tests/LocalTutor.Tests/FileInspectionAndConversion/ImageToolTests.cs` | PR #2 validation: FileInspectionAndConversion; ImageToolTests. Cover ImageMagick 7 ICC inspection behavior without allowing unsupported conversion. |
| Added | `tests/LocalTutor.Tests/FileOrganization/FindDuplicatesTests.cs` | PR #2 validation: FileOrganization; FindDuplicatesTests. Normalize fixture paths for Windows. |
| Added | `tests/LocalTutor.Tests/FileOrganization/OrganizationApplyTests.cs` | PR #2 validation: FileOrganization; OrganizationApplyTests. Normalize fixture paths for Windows. |
| Added | `tests/LocalTutor.Tests/FileOrganization/OrganizationNativeTests.cs` | PR #2 validation: FileOrganization; OrganizationNativeTests. |
| Added | `tests/LocalTutor.Tests/FileOrganization/OrganizationPreviewTests.cs` | PR #2 validation: FileOrganization; OrganizationPreviewTests. Normalize fixture paths for Windows. |
| Added | `tests/LocalTutor.Tests/Pdf/PdfToolTests.cs` | PR #2 validation: Pdf; PdfToolTests. |
| Added | `tests/LocalTutor.Tests/VideoDownload/ApprovedLiveVideoTests.cs` | PR #2 validation: VideoDownload; ApprovedLiveVideoTests. |
| Added | `tests/LocalTutor.Tests/VideoDownload/NativeVideoProcessTests.cs` | PR #2 validation: VideoDownload; NativeVideoProcessTests. |
| Added | `tests/LocalTutor.Tests/VideoDownload/VideoToolTests.cs` | PR #2 validation: VideoDownload; VideoToolTests. Reject other FFmpeg patch versions while accepting the pinned Windows build. |
| Added | `tests/LocalTutor.Tests/ZipArchive/ArchiveToolTests.cs` | PR #2 validation: ZipArchive; ArchiveToolTests. Normalize fixture paths for Windows. |
