   M S B A M L   `   `   `  ����A  =NetSet, Version=1.0.0.0, Culture=neutral, PublicKeyToken=nullclr-namespace:NetSetNetSet  5          NetSet.PingBox   5      W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      &localclr-namespace:NetSet     5        � ContextMenu  5	      �� 5
   
   .@�5      m� -   5         m�Name$ browserItem��5      $��Otwórz w przeglądarce��6,   6K   m� 5      -   5        $ portItem��5      $��Skanuj porty��6)   6?   m� 5      -   5        $ 
deleteItem��5      $��Przestań pingować��6+   6F   5      .��5      �  � ColumnDefinitions 5   
   �� 5      $��* �6   $	��128��6)   �� 5      $
��Auto �6   �� 5      $
��Auto �6   $��40��6,   �� 5      $��24 �6   $��24��6*   .+�5   
   � -   5         �Name$ polygon��5      $��1��6#   $��24��63   ���	#7F40E0FF���	#FF00C0FF �� Stretch$
 Fill=�6c   $��Bottom=�6r   �� 5   
   -   5         ��Name$ ipBlock��5      $��Square721 Cn BT)�6%   $��14(�6B   $	��0,5q�6P   �� 5   
   -   5        $ 	timeBlock��5      $��Square721 Cn BT)�6'   $��14(�6D   $	��0,5q�6R   $��2��6_   $��Oczekiwanie��6o   $��Right=�6�   �� 5   
   -   5         ��Name$ statusIndicator��5      $��3��6-   
�������$��Center=�6K   $��12��6h   
�������$��Center=�6�   $��12��6�    �� RadiusX$ 32��6�    �� RadiusY$ 32��6�   $��2��6�   5      5      