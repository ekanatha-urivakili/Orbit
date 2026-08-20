import { useState } from 'react'
import { Bell, ChevronRight, CircleUserRound, CreditCard, Grip, HelpCircle, LogOut, Search, Settings, Shield, SlidersHorizontal, UsersRound } from 'lucide-react'
import type { Profile, ThemePreference } from '../../api/types'
import type { SettingsSection } from '../../features/settings/SettingsView'
import { getInitials } from '../../lib/initials'
import * as auth from '../../api/auth'

interface HeaderProps {
  online: boolean
  profile?: Profile
  logoUrl?: string | null
  onCreateClick?: () => void
  onHomeClick: () => void
  onOpenSettings: (section: SettingsSection) => void
  onThemeChange: (theme: ThemePreference) => void
}

export function Header({ online, profile, logoUrl, onCreateClick, onHomeClick, onOpenSettings, onThemeChange }: HeaderProps) {
  const [openMenu, setOpenMenu] = useState<'settings' | 'profile' | null>(null)
  const [themeOpen, setThemeOpen] = useState(false)
  const initials = getInitials(profile?.displayName)
  const openSettings = (section: SettingsSection) => { setOpenMenu(null); onOpenSettings(section) }

  return (
    <header className="flex items-center justify-between px-3 md:px-4 h-12 bg-[#0052cc] text-white sticky top-0 z-50">
      <div className="flex items-center gap-2.5 shrink-0">
        <button className="p-1 hover:bg-white/20 rounded" aria-label="App switcher"><Grip size={18} /></button>
        <button onClick={onHomeClick} className="flex items-center gap-1.5 rounded px-1 py-0.5 font-bold text-lg tracking-tight hover:bg-white/10" aria-label="Orbit home">
          {logoUrl
            ? <img src={logoUrl} alt="" className="h-6 w-6 rounded object-contain bg-white" />
            : <span className="flex h-6 w-6 items-center justify-center rounded bg-white text-xs font-bold text-[#0052cc]">O</span>}
          Orbit
        </button>
      </div>

      <div className="flex-1 flex items-center justify-center max-w-2xl px-4 gap-3">
        <label className="relative flex items-center w-full bg-white/20 hover:bg-white/30 rounded-md h-8 transition-colors cursor-text">
          <Search size={15} className="absolute left-2.5 text-white/80" />
          <input
            readOnly
            onFocus={(event) => {
              event.target.blur()
              window.dispatchEvent(new CustomEvent('orbit:open-command-palette'))
            }}
            className="w-full h-full pl-8 pr-3 bg-transparent text-sm text-white placeholder-white/80 focus:outline-none cursor-text"
            placeholder="Search Orbit (issues, spaces, boards)"
            aria-label="Search Orbit"
          />
          <kbd className="absolute right-2.5 text-[10px] text-white/70 border border-white/30 rounded px-1 py-0.5 pointer-events-none">⌘K</kbd>
        </label>
        {onCreateClick && <button onClick={onCreateClick} className="bg-blue-600 hover:bg-blue-700 px-3 py-1.5 rounded font-semibold text-xs shadow-sm whitespace-nowrap">+ Create</button>}
      </div>

      <div className="relative flex items-center gap-1.5">
        {!online && <span className="text-[10px] bg-red-500/30 px-1.5 py-0.5 rounded">Offline</span>}
        <button onClick={() => openSettings('notifications')} className="p-1 hover:bg-white/20 rounded" aria-label="Notifications" title="Notification settings"><Bell size={18} /></button>
        <button onClick={() => openSettings('profile')} className="p-1 hover:bg-white/20 rounded" aria-label="Help" title="Preferences & Help"><HelpCircle size={18} /></button>
        <button onClick={() => { setOpenMenu((current) => current === 'settings' ? null : 'settings'); setThemeOpen(false) }} className="p-1 hover:bg-white/20 rounded" aria-label="Settings"><Settings size={18} /></button>
        <button onClick={() => { setOpenMenu((current) => current === 'profile' ? null : 'profile'); setThemeOpen(false) }} className="w-7 h-7 rounded-full bg-orange-400 text-slate-900 flex items-center justify-center text-xs font-bold overflow-hidden" aria-label="Profile menu">
          {profile?.avatarUrl ? <img src={profile.avatarUrl} alt={profile.displayName} className="w-full h-full object-cover" /> : initials}
        </button>

        {openMenu === 'settings' && <SettingsMenu onSelect={openSettings} />}
        {openMenu === 'profile' && <ProfileMenu profile={profile} themeOpen={themeOpen} setThemeOpen={setThemeOpen} onSelect={openSettings} onHomeClick={() => { setOpenMenu(null); onHomeClick() }} onThemeChange={(theme) => { onThemeChange(theme); setThemeOpen(false); setOpenMenu(null) }} />}
      </div>
    </header>
  )
}

function SettingsMenu({ onSelect }: { onSelect: (section: SettingsSection) => void }) {
  return <div className="absolute right-10 top-11 w-[420px] max-w-[90vw] rounded-xl border border-gray-200 bg-white p-3 text-gray-900 shadow-2xl">
    <MenuHeading>Personal Orbit settings</MenuHeading>
    <MenuItem icon={<CircleUserRound size={19} />} title="General settings" detail="Manage language, time zone, theme, and preferences" onClick={() => onSelect('profile')} />
    <MenuItem icon={<Bell size={19} />} title="Notification settings" detail="Manage email and in-app notifications" onClick={() => onSelect('notifications')} />
    <MenuHeading>Orbit admin settings</MenuHeading>
    <MenuItem icon={<SlidersHorizontal size={19} />} title="System and spaces" detail="Workspace configuration, defaults, and member capabilities" onClick={() => onSelect('workspace')} />
    <MenuItem icon={<Settings size={19} />} title="Work items" detail="Configure work types, defaults, fields, and project behaviour" onClick={() => onSelect('project')} />
    <MenuHeading>Account administration</MenuHeading>
    <MenuItem icon={<UsersRound size={19} />} title="User management" detail="Identity and access controls" onClick={() => onSelect('members')} />
    <MenuItem icon={<CreditCard size={19} />} title="Billing" detail="Subscription management is not enabled in local mode" disabled />
  </div>
}

function ProfileMenu({ profile, themeOpen, setThemeOpen, onSelect, onHomeClick, onThemeChange }: { profile?: Profile; themeOpen: boolean; setThemeOpen: (open: boolean) => void; onSelect: (section: SettingsSection) => void; onHomeClick: () => void; onThemeChange: (theme: ThemePreference) => void }) {
  const initials = getInitials(profile?.displayName)
  return <div className="absolute right-0 top-11 w-[320px] max-w-[90vw] rounded-xl border border-gray-200 bg-white p-3 text-gray-900 shadow-2xl">
    <div className="mb-2 flex items-center gap-3 rounded-lg bg-gray-50 p-3">
      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-orange-400 text-lg font-bold overflow-hidden">
        {profile?.avatarUrl ? <img src={profile.avatarUrl} alt={profile.displayName} className="w-full h-full object-cover" /> : initials}
      </span>
      <div className="min-w-0"><p className="truncate font-semibold">{profile?.displayName ?? 'Orbit user'}</p><p className="truncate text-xs text-gray-500">{profile?.email ?? 'Loading account…'}</p></div>
    </div>
    <MenuItem icon={<CircleUserRound size={19} />} title="Profile" onClick={() => onSelect('profile')} />
    <MenuItem icon={<Shield size={19} />} title="Account settings" onClick={() => onSelect('security')} />
    <div className="relative"><MenuItem icon={<SlidersHorizontal size={19} />} title="Theme" trailing={<ChevronRight size={17} />} onClick={() => setThemeOpen(!themeOpen)} />{themeOpen && <div className="mx-2 mb-2 rounded-lg border border-gray-200 bg-gray-50 p-1">{(['Light', 'Dark', 'System'] as ThemePreference[]).map((theme) => <button key={theme} onClick={() => onThemeChange(theme)} className={`flex w-full rounded-md px-3 py-2 text-left text-sm hover:bg-white ${profile?.theme === theme ? 'font-semibold text-blue-700' : ''}`}>{theme === 'System' ? 'Match browser' : theme}</button>)}</div>}</div>
    <MenuItem icon={<HelpCircle size={19} />} title="Open Quickstart / Home" onClick={onHomeClick} />
    <div className="my-2 border-t border-gray-200" />
    <MenuItem icon={<LogOut size={19} />} title="Log out" onClick={() => auth.logout()} />
  </div>
}

function MenuHeading({ children }: { children: string }) { return <p className="px-3 pb-1 pt-2 text-xs font-bold text-gray-500">{children}</p> }

function MenuItem({ icon, title, detail, trailing, onClick, disabled = false }: { icon: React.ReactNode; title: string; detail?: string; trailing?: React.ReactNode; onClick?: () => void; disabled?: boolean }) {
  return <button disabled={disabled} onClick={onClick} className="flex w-full items-start gap-3 rounded-lg px-3 py-2.5 text-left hover:bg-blue-50 disabled:cursor-not-allowed disabled:opacity-45"><span className="mt-0.5">{icon}</span><span className="min-w-0 flex-1"><span className="block text-sm font-medium">{title}</span>{detail && <span className="mt-0.5 block text-xs leading-4 text-gray-500">{detail}</span>}</span>{trailing}</button>
}
