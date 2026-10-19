import React, { useId, useRef } from 'react';

export function ReviewTabs({ tabs, active, onChange, label, children }: {
  tabs: { id: string; label: string }[]; active: string; onChange: (id: string) => void;
  label: string; children: React.ReactNode;
}) {
  const id = useId();
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const select = (index: number) => { onChange(tabs[index].id); refs.current[index]?.focus(); };
  return <>
    <div role="tablist" aria-label={label} className="review-tabs">
      {tabs.map((tab, index) => <button key={tab.id} ref={node => { refs.current[index] = node; }}
        type="button" role="tab" id={`${id}-${tab.id}`} aria-controls={`${id}-panel`}
        aria-selected={active === tab.id} tabIndex={active === tab.id ? 0 : -1}
        onClick={() => onChange(tab.id)} onKeyDown={event => {
          const offset = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
          if (offset) { event.preventDefault(); select((index + offset + tabs.length) % tabs.length); }
          if (event.key === 'Home' || event.key === 'End') { event.preventDefault(); select(event.key === 'Home' ? 0 : tabs.length - 1); }
        }}>{tab.label}</button>)}
    </div>
    <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-${active}`} tabIndex={0} className="review-tab-panel">
      {children}
    </div>
  </>;
}
