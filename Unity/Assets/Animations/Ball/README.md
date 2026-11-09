# Death Ball upper-body animations

These four AnimationClips and BallUpperBody.mask are generated from hand-set Humanoid muscle curves in `BallAnimationBuilder`, which the Unity AI Assistant wrote. No Synty clip, mesh, Avatar, bone animation, or other licensed asset was copied, sampled or baked to create them.

They're covered by the project's licence: see the [root README](../../../../README.md#license).

## Clips

- BallCatch: 0.32 seconds, immediate reach/receive, non-looping.
- BallHold: 1 second, restrained two-handed carry, looping.
- BallWindUp: 1 second of pose progression, sampled using BallCharge rather than wall-clock playback.
- BallRelease: 0.25 seconds, quick forward throw/follow-through, non-looping.

Builders.BallAnimationBuilder regenerates these standalone assets without sampling the locomotion clips. Builders.TeamArtBuilder invokes it for each generated controller. The Ball override layer excludes root and legs and enables IK Pass. CharacterArt installs CharacterBallIK on each runtime model's Animator. IK maintains hand contact and corrects residual aim using bone-scaled targets. BallVisual follows the evaluated palms and retains its pickup/release blending; Ball.HoldPosition and all server physics remain unchanged.

Synty locomotion, models and team output remain in their original licensed/ignored folders. Only the four shared clips and one mask are added to the runtime animation payload; no FBX, texture, rig or animation pack is imported.
