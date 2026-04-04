using Content.Shared.Actions;

namespace Content.Shared.Blob;

public sealed partial class BlobDeployActionEvent : WorldTargetActionEvent;

public sealed partial class BlobSpreadActionEvent : WorldTargetActionEvent;

public sealed partial class BlobConsumeActionEvent : WorldTargetActionEvent;

public sealed partial class BlobRepairActionEvent : WorldTargetActionEvent;

public sealed partial class BlobAbsorbActionEvent : WorldTargetActionEvent;

public sealed partial class BlobPromoteNucleusActionEvent : WorldTargetActionEvent;

public sealed partial class BlobChangeColorActionEvent : InstantActionEvent;

public sealed partial class BlobDevourItemActionEvent : WorldTargetActionEvent;

public sealed partial class BlobBuildBridgeActionEvent : WorldTargetActionEvent;

public sealed partial class BlobEvoPurchaseActionEvent : InstantActionEvent;
