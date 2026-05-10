using Unity.Netcode.Components;

/// <summary>
/// A NetworkAnimator subclass that lets the OWNER drive animation parameters
/// (instead of the server). Use this on player NetworkObjects so that the client
/// who controls Player 2 actually drives Player 2's animations on every screen.
///
/// Replace the default "Network Animator" component with this on each player.
/// </summary>
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
