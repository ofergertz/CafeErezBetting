import React from 'react';
import type { LucideIcon } from 'lucide-react';

interface EmptyStateProps {
  icon: LucideIcon;
  message: string;
  subMessage?: string;
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = ({ icon: Icon, message, subMessage, className = '' }) => {
  return (
    <div className={`flex flex-col items-center justify-center py-12 px-4 text-center ${className}`}>
      <div className="mb-4 text-gray-300">
        <Icon size={48} strokeWidth={1} />
      </div>
      <p className="text-gray-500 font-medium text-base">{message}</p>
      {subMessage && <p className="text-gray-400 text-sm mt-1">{subMessage}</p>}
    </div>
  );
};

export default EmptyState;
