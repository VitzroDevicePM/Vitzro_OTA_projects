using System;
using System.Windows.Input;

namespace Vitzro_OTA_projects.Model
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /**
         * BRIEF  명령을 현재 상태에서 실행할 수 있는지를 결정하는 함수
         * PARAM  parameter: 명령어
         * THROWS 
         * RETURN 성공시 true, 실패시 false
         */
        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        /**
         * BRIEF  명령이 호출될 때 호출하는 함수
         * PARAM  parameter: 전달 변수
         * THROWS 
         * RETURN void
         */
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged;
        /**
         * BRIEF  MVVM 패턴상 UI 싱크를 위해 실행 가능 상태가 변경되었음을 UI에 알리는 함수
         * PARAM  
         * THROWS 
         * RETURN void
         */
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
