import { useState } from 'react'
import { Bell, ChevronRight, CircleUserRound, CreditCard, Grip, HelpCircle, LogOut, Plus, Search, Settings, Shield, SlidersHorizontal, UsersRound } from 'lucide-react'
import type { AccountWorkspace, Profile, ThemePreference } from '../../api/types'
import type { SettingsSection } from '../../features/settings/SettingsView'
import { getInitials } from '../../lib/initials'

interface HeaderProps {
  online: boolean
  profile?: Profile
  onCreateClick?: () => void
  onHomeClick: () => void
  onOpenSettings: (section: SettingsSection) => void
  onThemeChange: (theme: ThemePreference) => void
  workspaces?: AccountWorkspace[]
  currentWorkspaceId?: string
  switchingWorkspace?: boolean
  onWorkspaceChange: (workspaceId: string) => void
  onCreateWorkspace?: () => void
}

export function Header({ online, profile, onCreateClick, onHomeClick, onOpenSettings, onThemeChange, workspaces, currentWorkspaceId, switchingWorkspace, onWorkspaceChange, onCreateWorkspace }: HeaderProps) {
  const [openMenu, setOpenMenu] = useState<'settings' | 'profile' | null>(null)
  const [themeOpen, setThemeOpen] = useState(false)
  const initials = getInitials(profile?.displayName)
  const openSettings = (section: SettingsSection) => { setOpenMenu(null); onOpenSettings(section) }

  return (
    <header className="flex items-center justify-between px-4 h-14 bg-[#0052cc] text-white sticky top-0 z-50">
      <div className="flex items-center gap-3">
        <button className="p-1.5 hover:bg-white/20 rounded" aria-label="App switcher"><Grip size={20} /></button>
        <button onClick={onHomeClick} className="flex items-center gap-2 rounded px-1 py-1 font-bold text-xl tracking-tight hover:bg-white/10" aria-label="Orbit home">
          <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-white text-sm text-[#0052cc]">O</span>Orbit
        </button>
        {!!workspaces?.length && (
          <select
            aria-label="Current workspace"
            value={currentWorkspaceId ?? ''}
            disabled={switchingWorkspace}
            onChange={(event) => onWorkspaceChange(event.target.value)}
            className="max-w-52 rounded border border-white/30 bg-white/15 px-2 py-1 text-sm text-white disabled:opacity-60"
          >
            {workspaces.map((workspace) => (
              <option key={workspace.id} value={workspace.id} className="text-gray-900">{workspace.name}</option>
            ))}
          </select>
        )}
        {onCreateWorkspace && (
          <button onClick={onCreateWorkspace} className="rounded p-1.5 hover:bg-white/20" aria-label="Create workspace">
            <Plus size={18} />
          </button>
        )}
      </div>

      <div className="flex-1 flex items-center justify-center max-w-2xl px-4 gap-4">
        <label className="relative flex items-center w-full max-w-md bg-white/20 hover:bg-white/30 rounded-md h-8"><Search size={16} className="absolute left-2 text-white/80" /><input className="w-full h-full pl-8 pr-3 bg-transparent text-sm text-white placeholder-white/80 focus:outline-none" placeholder="Search" /></label>
        {onCreateClick && <button onClick={onCreateClick} className="bg-blue-600 hover:bg-blue-700 px-4 py-1.5 rounded font-medium text-sm shadow-sm">+ Create</button>}
      </div>

      <div className="relative flex items-center gap-2">
        {!online && <span className="text-xs bg-red-500/30 px-2 py-1 rounded">Offline</span>}
        <button className="p-1.5 hover:bg-white/20 rounded" aria-label="Notifications"><Bell size={20} /></button>
        <button className="p-1.5 hover:bg-white/20 rounded" aria-label="Help"><HelpCircle size={20} /></button>
        <button onClick={() => { setOpenMenu((current) => current === 'settings' ? null : 'settings'); setThemeOpen(false) }} className="p-1.5 hover:bg-white/20 rounded" aria-label="Settings"><Settings size={20} /></button>
        <button onClick={() => { setOpenMenu((current) => current === 'profile' ? null : 'profile'); setThemeOpen(false) }} className="w-8 h-8 rounded-full bg-orange-400 text-slate-900 flex items-center justify-center text-xs font-bold" aria-label="Profile menu">{initials}</button>

        {openMenu === 'settings' && <SettingsMenu onSelect={openSettings} />}
        {openMenu === 'profile' && <ProfileMenu profile={profile} themeOpen={themeOpen} setThemeOpen={setThemeOpen} onSelect={openSettings} onThemeChange={(theme) => { onThemeChange(theme); setThemeOpen(false); setOpenMenu(null) }} />}
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

function ProfileMenu({ profile, themeOpen, setThemeOpen, onSelect, onThemeChange }: { profile?: Profile; themeOpen: boolean; setThemeOpen: (open: boolean) => void; onSelect: (section: SettingsSection) => void; onThemeChange: (theme: ThemePreference) => void }) {
  const initials = getInitials(profile?.displayName)
  return <div className="absolute right-0 top-11 w-[320px] max-w-[90vw] rounded-xl border border-gray-200 bg-white p-3 text-gray-900 shadow-2xl">
    <div className="mb-2 flex items-center gap-3 rounded-lg bg-gray-50 p-3"><span className="flex h-12 w-12 items-center justify-center rounded-full bg-orange-400 text-lg font-bold">{initials}</span><div className="min-w-0"><p className="truncate font-semibold">{profile?.displayName ?? 'Orbit user'}</p><p className="truncate text-xs text-gray-500">{profile?.email ?? 'Loading account…'}</p></div></div>
    <MenuItem icon={<CircleUserRound size={19} />} title="Profile" onClick={() => onSelect('profile')} />
    <MenuItem icon={<Shield size={19} />} title="Account settings" onClick={() => onSelect('security')} />
    <div className="relative"><MenuItem icon={<SlidersHorizontal size={19} />} title="Theme" trailing={<ChevronRight size={17} />} onClick={() => setThemeOpen(!themeOpen)} />{themeOpen && <div className="mx-2 mb-2 rounded-lg border border-gray-200 bg-gray-50 p-1">{(['Light', 'Dark', 'System'] as ThemePreference[]).map((theme) => <button key={theme} onClick={() => onThemeChange(theme)} className={`flex w-full rounded-md px-3 py-2 text-left text-sm hover:bg-white ${profile?.theme === theme ? 'font-semibold text-blue-700' : ''}`}>{theme === 'System' ? 'Match browser' : theme}</button>)}</div>}</div>
    <MenuItem icon={<HelpCircle size={19} />} title="Open Quickstart" disabled />
    <div className="my-2 border-t border-gray-200" />
    <MenuItem icon={<UsersRound size={19} />} title="Switch account" disabled />
    <MenuItem icon={<LogOut size={19} />} title="Log out" disabled />
  </div>
}

function MenuHeading({ children }: { children: string }) { return <p className="px-3 pb-1 pt-2 text-xs font-bold text-gray-500">{children}</p> }

function MenuItem({ icon, title, detail, trailing, onClick, disabled = false }: { icon: React.ReactNode; title: string; detail?: string; trailing?: React.ReactNode; onClick?: () => void; disabled?: boolean }) {
  return <button disabled={disabled} onClick={onClick} className="flex w-full items-start gap-3 rounded-lg px-3 py-2.5 text-left hover:bg-blue-50 disabled:cursor-not-allowed disabled:opacity-45"><span className="mt-0.5">{icon}</span><span className="min-w-0 flex-1"><span className="block text-sm font-medium">{title}</span>{detail && <span className="mt-0.5 block text-xs leading-4 text-gray-500">{detail}</span>}</span>{trailing}</button>
}
