import { EmptyState } from '../components/ui';
import Announcements from './announcements/Announcements';
import Attendance from './attendance/Attendance';
import ClassScreens from './classes/ClassScreens';
import Events from './events/Events';
import Fees from './fees/Fees';
import People from './people/People';
import Reports from './reports/Reports';
import Settings, { Permissions } from './settings/Settings';
import SchoolYears from './school/SchoolYears';

export default function PreviewRoutes({ path, navigate }: { path: string; navigate: (url: string) => void }) {
  const [, , section, segment, action] = path.split('/');
  const id = segment === 'new' ? undefined : segment;
  const mode = segment === 'new' ? 'new' : action;
  const props = { id, mode, navigate };
  switch (section) {
    case 'students': return <People key={path} kind="students" {...props} />;
    case 'teachers': return <People key={path} kind="teachers" {...props} />;
    case 'classes': return <ClassScreens key={path} {...props} />;
    case 'attendance': return <Attendance />;
    case 'events': return <Events key={path} {...props} />;
    case 'announcements': return <Announcements key={path} {...props} />;
    case 'fees': return <Fees key={path} {...props} />;
    case 'reports': return <Reports />;
    case 'settings': return segment === 'years' ? <SchoolYears preview navigate={navigate} /> : <Settings key={path} navigate={navigate} />;
    case 'permissions': return <Permissions navigate={navigate} />;
    default: return <EmptyState title="Màn hình chưa có bản thiết kế">Chưa có ảnh Trung tâm hỗ trợ; không tự đoán giao diện.</EmptyState>;
  }
}
