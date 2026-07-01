import './MobileLayout.css';
/**
 * Mobile-first layout wrapper.
 * Centers content in a phone-sized container with
 * a subtle background, matching the SafeRide mobile UI.
 */
function MobileLayout({ children }) {
    return (<div className="mobile-layout" id="mobile-layout">
      <div className="mobile-frame" id="mobile-frame">
        {children}
      </div>
    </div>);
}
export default MobileLayout;
